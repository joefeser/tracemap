import ts from "typescript";
import { CodeFact, EvidenceTiers, FactTypes, ScanManifest } from "../facts/Models";
import { createEvidence, createFact } from "../facts/FactFactory";
import { RuleIds, ScannerVersions } from "../facts/RuleIds";
import { hash } from "../util/Hash";

type ValueClass = "string" | "integer" | "decimal" | "number" | "boolean" | "date-string" | "datetime-string" | "array" | "object" | "unknown";
type Confidence = "high" | "medium" | "low";

interface SourceSpan {
  filePath: string;
  startLine: number;
  endLine: number;
  startOffset: number;
  endOffset: number;
  snippetSha256: string;
}

interface ReasonEvidence extends SourceSpan {
  ruleId: typeof RuleIds.Base44UiInputSemantics;
  kind: "native-input-type" | "component-prop" | "parse-cast-function" | "label-context-clue" | "submit-handler-propagation";
  detail: string;
}

interface PayloadBinding {
  entityName: string;
  operationName: string;
  operationEvidenceId: string;
  fieldName: string;
  bindingPath: string;
  bindingRootIdentity: string;
  payloadRoot: string;
  valueClass: ValueClass;
  reasons: ReasonEvidence[];
  evidence: SourceSpan;
  scope: ts.Node;
}

interface ControlCandidate {
  node: ts.JsxOpeningLikeElement;
  controlKind: "input" | "select" | "textarea" | "component";
  componentName: string;
  fieldBinding: string;
  valueBinding: string;
  valueBindingRootIdentity: string;
  dynamicTypeUnresolved: boolean;
  valueClass: ValueClass;
  reasons: ReasonEvidence[];
  representativeValues: string[];
  validation: Record<string, string | boolean>;
  scope: ts.Node;
}

interface UiSemanticsContract {
  schemaVersion: "88mph.base44-ui-input-semantics.v1";
  controlKind: "input" | "select" | "textarea" | "component" | "submitted-value";
  componentName: string;
  fieldBinding: string;
  valueBinding: string;
  submittedEntity: string;
  submittedField: string;
  operationName: string;
  operationEvidenceId: string;
  valueClass: ValueClass;
  reasons: ReasonEvidence[];
  confidence: Confidence;
  correlationStatus: "proven" | "partial" | "unresolved";
  unresolvedCorrelationReason: string;
  representativeValues: string[];
  optionAuthority: "representative-only" | "none";
  validation: Record<string, string | boolean>;
  validationAuthority: "test-validation-only";
  storageAuthority: "widening-only-never-narrowing";
}

const mutationPayloadIndex = new Map<string, number>([["create", 0], ["update", 1], ["bulkCreate", 0]]);
const validationProps = ["required", "min", "max", "pattern", "maxLength"] as const;
const relevantProps = new Set(["type", "inputMode", "step", "min", "max", "pattern", "required", "maxLength", "value", "checked", "name", "onChange", "onValueChange"]);
const decimalClues = /(?:^|[_-])(cost|price|rate|amount|currency|percent|percentage|weight|dimension|duration)(?:$|[_-])/iu;
const numberClues = /(?:^|[_-])(quantity|qty|count|number)(?:$|[_-])/iu;
const maxRepresentativeValues = 64;

export function extractBase44UiInputFacts(
  manifest: ScanManifest,
  source: ts.SourceFile,
  filePath: string,
  sourceText: string,
  existingFacts: readonly CodeFact[]
): CodeFact[] {
  const payloadBindings = collectPayloadBindings(source, filePath, existingFacts);
  const controls = collectControls(source, filePath);
  const facts = payloadBindings
    .filter((binding) => binding.valueClass !== "unknown" || binding.reasons.some((reason) => reason.kind === "label-context-clue"))
    .map((binding) => uiFact(manifest, source, sourceText, binding.evidence, {
      schemaVersion: "88mph.base44-ui-input-semantics.v1",
      controlKind: "submitted-value",
      componentName: "payload-field",
      fieldBinding: binding.bindingPath,
      valueBinding: binding.bindingPath,
      submittedEntity: binding.entityName,
      submittedField: binding.fieldName,
      operationName: binding.operationName,
      operationEvidenceId: binding.operationEvidenceId,
      valueClass: binding.valueClass,
      reasons: normalizeReasons(binding.reasons),
      confidence: binding.reasons.some((reason) => reason.kind === "parse-cast-function") ? "high" : "medium",
      correlationStatus: "proven",
      unresolvedCorrelationReason: "",
      representativeValues: [],
      optionAuthority: "none",
      validation: {},
      validationAuthority: "test-validation-only",
      storageAuthority: "widening-only-never-narrowing"
    }));

  for (const control of controls) {
    const matches = control.dynamicTypeUnresolved ? [] : matchPayloadBindings(control, payloadBindings);
    const distinctTargets = new Map(matches.map((match) => [`${match.entityName}\0${match.fieldName}\0${match.operationEvidenceId}`, match]));
    const entityFields = new Set([...distinctTargets.values()].map((match) => `${match.entityName}\0${match.fieldName}`));
    const provenMatches: Array<PayloadBinding | null> = entityFields.size === 1 ? [...distinctTargets.values()] : [null];
    const unresolvedCorrelationReason = entityFields.size === 1 ? "" : distinctTargets.size > 1
      ? "multiple-submitted-payload-targets"
      : "no-proven-submitted-payload-correlation";
    for (const proven of provenMatches) {
      const reasons = normalizeReasons([
        ...control.reasons,
        ...(proven ? [reason(proven.evidence, "submit-handler-propagation", `${proven.entityName}.${proven.fieldName}`)] : [])
      ]);
      const valueClass = proven?.valueClass && proven.valueClass !== "unknown"
        ? mergeSubmittedValueClass(control.valueClass, proven.valueClass)
        : control.valueClass;
      const evidence = span(control.node, source, filePath);
      const fieldBinding = control.fieldBinding || control.valueBinding;
      facts.push(uiFact(manifest, source, sourceText, evidence, {
        schemaVersion: "88mph.base44-ui-input-semantics.v1",
        controlKind: control.controlKind,
        componentName: control.componentName,
        fieldBinding,
        valueBinding: control.valueBinding,
        submittedEntity: proven?.entityName ?? "",
        submittedField: proven?.fieldName ?? "",
        operationName: proven?.operationName ?? "",
        operationEvidenceId: proven?.operationEvidenceId ?? "",
        valueClass,
        reasons,
        confidence: proven ? (valueClass === "unknown" ? "medium" : "high") : "low",
        correlationStatus: proven ? "proven" : fieldBinding ? "partial" : "unresolved",
        unresolvedCorrelationReason,
        representativeValues: control.representativeValues,
        optionAuthority: control.representativeValues.length > 0 ? "representative-only" : "none",
        validation: control.validation,
        validationAuthority: "test-validation-only",
        storageAuthority: "widening-only-never-narrowing"
      }));
    }
  }
  return facts;
}

function collectPayloadBindings(source: ts.SourceFile, filePath: string, facts: readonly CodeFact[]): PayloadBinding[] {
  const calls = new Map<number, ts.CallExpression>();
  const visit = (node: ts.Node): void => {
    if (ts.isCallExpression(node)) calls.set(node.getStart(source), node);
    ts.forEachChild(node, visit);
  };
  visit(source);
  const bindings: PayloadBinding[] = [];
  for (const fact of facts.filter((candidate) => candidate.factType === FactTypes.Base44EntityOperation
    && candidate.evidence.filePath === filePath
    && candidate.evidenceTier !== EvidenceTiers.Tier4Unknown
    && !candidate.properties.sdkIdentityGap
    && !candidate.properties.entitySelectorGap
    && mutationPayloadIndex.has(candidate.properties.operationName))) {
    const call = calls.get(Number(fact.properties.callsiteStartOffset));
    const argumentIndex = mutationPayloadIndex.get(fact.properties.operationName);
    if (!call || argumentIndex === undefined || !call.arguments[argumentIndex]) continue;
    collectPayloadExpression(call.arguments[argumentIndex], {
      source, filePath, entityName: fact.properties.entityName, operationName: fact.properties.operationName,
      operationEvidenceId: fact.properties.operationEvidenceId, scope: componentScope(call), visited: new Set()
    }, bindings);
  }
  return normalizePayloadBindings(bindings);
}

interface PayloadContext {
  source: ts.SourceFile;
  filePath: string;
  entityName: string;
  operationName: string;
  operationEvidenceId: string;
  scope: ts.Node;
  visited: Set<number>;
}

function collectPayloadExpression(expression: ts.Expression, context: PayloadContext, output: PayloadBinding[]): void {
  const value = unwrap(expression);
  if (context.visited.has(value.pos)) return;
  context.visited.add(value.pos);
  if (ts.isObjectLiteralExpression(value)) {
    for (const property of value.properties) {
      if (ts.isPropertyAssignment(property)) {
        const fieldName = staticPropertyName(property.name);
        if (!fieldName) continue;
        addPayloadBinding(fieldName, property.initializer, property, context, output);
      } else if (ts.isShorthandPropertyAssignment(property)) {
        addPayloadBinding(property.name.text, property.name, property, context, output);
      } else if (ts.isSpreadAssignment(property)) {
        collectPayloadExpression(property.expression, { ...context, visited: new Set(context.visited) }, output);
      }
    }
    return;
  }
  if (ts.isArrayLiteralExpression(value)) {
    for (const element of value.elements) if (ts.isExpression(element)) collectPayloadExpression(element, { ...context, visited: new Set(context.visited) }, output);
    return;
  }
  if (ts.isConditionalExpression(value)) {
    collectPayloadExpression(value.whenTrue, { ...context, visited: new Set(context.visited) }, output);
    collectPayloadExpression(value.whenFalse, { ...context, visited: new Set(context.visited) }, output);
    return;
  }
  if (ts.isCallExpression(value) && ts.isPropertyAccessExpression(value.expression) && value.expression.name.text === "map") {
    const callback = value.arguments[0];
    if (callback && (ts.isArrowFunction(callback) || ts.isFunctionExpression(callback))) {
      const returned = ts.isBlock(callback.body)
        ? callback.body.statements.find(ts.isReturnStatement)?.expression
        : callback.body;
      if (returned) collectPayloadExpression(returned, { ...context, visited: new Set(context.visited) }, output);
    }
    return;
  }
  if (ts.isIdentifier(value)) {
    const declaration = resolveConstDeclaration(value, context.source, context.scope);
    if (declaration?.initializer) {
      if (hasBindingMutationBetween(value.text, declaration, value, context.source, context.scope)) {
        output.push(payloadBinding(context, "*", value.text, bindingRootIdentity(value, context.source), value.text, "unknown", [], value));
        return;
      }
      collectPayloadExpression(declaration.initializer, { ...context, visited: new Set(context.visited) }, output);
      return;
    }
    output.push(payloadBinding(context, "*", value.text, bindingRootIdentity(value, context.source), value.text, "unknown", [], value));
  }
}

function addPayloadBinding(fieldName: string, expression: ts.Expression, evidenceNode: ts.Node, context: PayloadContext, output: PayloadBinding[]): void {
  const bindingPath = expressionBindingPath(expression, context.source);
  const bindingRoot = expressionBindingRootIdentity(expression, context.source);
  const inferred = inferExpressionValueClass(expression, context.source, context.filePath, fieldName);
  output.push(payloadBinding(context, fieldName, bindingPath, bindingRoot, "", inferred.valueClass, inferred.reasons, evidenceNode));
}

function payloadBinding(context: PayloadContext, fieldName: string, bindingPath: string, bindingRootIdentityValue: string, payloadRoot: string, valueClass: ValueClass, reasons: ReasonEvidence[], node: ts.Node): PayloadBinding {
  return {
    entityName: context.entityName,
    operationName: context.operationName,
    operationEvidenceId: context.operationEvidenceId,
    fieldName,
    bindingPath,
    bindingRootIdentity: bindingRootIdentityValue,
    payloadRoot,
    valueClass,
    reasons,
    evidence: span(node, context.source, context.filePath),
    scope: context.scope
  };
}

function collectControls(source: ts.SourceFile, filePath: string): ControlCandidate[] {
  const controls: ControlCandidate[] = [];
  const visit = (node: ts.Node): void => {
    if (ts.isJsxOpeningElement(node) || ts.isJsxSelfClosingElement(node)) {
      const componentName = node.tagName.getText(source);
      const attributes = jsxAttributes(node);
      const controlKind = componentName === "input" ? "input" : componentName === "select" ? "select" : componentName === "textarea" ? "textarea" : "component";
      const relevant = controlKind !== "component" || isWrappedControlComponent(componentName, attributes);
      if (relevant) {
        const fieldBinding = literalAttribute(attributes.get("name")) ?? setterFieldBinding(attributes.get("onChange"), source);
        const valueBindingExpression = expressionAttribute(attributes.get("value")) ?? expressionAttribute(attributes.get("checked"));
        const valueBinding = valueBindingExpression ? expressionBindingPath(valueBindingExpression, source) : "";
        const inferred = inferControlValueClass(controlKind, attributes, source, filePath, node, fieldBinding || valueBinding || componentName);
        controls.push({
          node, controlKind, componentName, fieldBinding: fieldBinding ?? "", valueBinding,
          valueBindingRootIdentity: valueBindingExpression ? expressionBindingRootIdentity(valueBindingExpression, source) : "",
          dynamicTypeUnresolved: controlKind === "input" && attributes.has("type") && literalAttribute(attributes.get("type")) === null,
          valueClass: inferred.valueClass, reasons: inferred.reasons,
          representativeValues: controlKind === "select" || /select/iu.test(componentName) ? selectOptionValues(node, source) : [],
          validation: validationEvidence(attributes)
          , scope: componentScope(node)
        });
      }
    }
    ts.forEachChild(node, visit);
  };
  visit(source);
  return controls;
}

function isWrappedControlComponent(componentName: string, attributes: Map<string, ts.JsxAttribute>): boolean {
  const leaf = componentName.split(".").at(-1) ?? componentName;
  if (!/^[A-Z]/u.test(leaf) || ![...attributes.keys()].some((name) => relevantProps.has(name))) return false;
  if (/(?:input|select|textarea|checkbox|datepicker|dateinput|field|switch|radio|slider|combobox)$/iu.test(leaf)) return true;
  const hasBinding = attributes.has("value") || attributes.has("checked") || attributes.has("name");
  return hasBinding && (attributes.has("onChange") || attributes.has("onValueChange"));
}

function matchPayloadBindings(control: ControlCandidate, payloads: PayloadBinding[]): PayloadBinding[] {
  return payloads.flatMap((payload) => {
    if (payload.scope !== control.scope) return [];
    if (control.valueBinding && payload.bindingPath === control.valueBinding
      && sameBindingRoot(control.valueBindingRootIdentity, payload.bindingRootIdentity)) return [payload];
    if (payload.payloadRoot && control.valueBinding.startsWith(`${payload.payloadRoot}.`)
      && sameBindingRoot(control.valueBindingRootIdentity, payload.bindingRootIdentity)) {
      const fieldName = control.valueBinding.slice(payload.payloadRoot.length + 1);
      return payload.fieldName === "*" ? [{ ...payload, fieldName }] : payload.fieldName === fieldName ? [payload] : [];
    }
    return control.fieldBinding && payload.fieldName === control.fieldBinding
      && (!control.valueBinding || (payload.bindingPath === control.valueBinding
        && sameBindingRoot(control.valueBindingRootIdentity, payload.bindingRootIdentity))) ? [payload] : [];
  });
}

function inferControlValueClass(controlKind: ControlCandidate["controlKind"], attributes: Map<string, ts.JsxAttribute>, source: ts.SourceFile, filePath: string, node: ts.JsxOpeningLikeElement, clue: string): { valueClass: ValueClass; reasons: ReasonEvidence[] } {
  const typeAttribute = attributes.get("type");
  const dynamicNativeType = controlKind === "input" && typeAttribute !== undefined && literalAttribute(typeAttribute) === null;
  const type = dynamicNativeType ? "" : literalAttribute(typeAttribute)?.toLowerCase() ?? "";
  const inputMode = literalAttribute(attributes.get("inputMode"))?.toLowerCase() ?? "";
  const step = literalAttribute(attributes.get("step"))?.toLowerCase() ?? "";
  const componentLeaf = node.tagName.getText(source).split(".").at(-1)?.toLowerCase() ?? "";
  let valueClass: ValueClass = controlKind === "input" || controlKind === "textarea" || controlKind === "select" ? "string"
    : ["input", "textarea", "select", "combobox", "radio"].includes(componentLeaf) ? "string"
    : /(?:checkbox|switch)$/u.test(componentLeaf) ? "boolean"
    : /datetime(?:picker|input)$/u.test(componentLeaf) ? "datetime-string"
    : /date(?:picker|input)$/u.test(componentLeaf) ? "date-string"
    : /(?:numberinput|slider)$/u.test(componentLeaf) ? "number"
    : "unknown";
  const reasons: ReasonEvidence[] = [];
  const kind = controlKind === "component" ? "component-prop" : "native-input-type";
  if (dynamicNativeType) valueClass = "unknown";
  else if (type === "checkbox") valueClass = "boolean";
  else if (type === "date") valueClass = "date-string";
  else if (type === "time") valueClass = "string";
  else if (type === "datetime-local") valueClass = "datetime-string";
  else if (inputMode === "decimal") valueClass = "decimal";
  else if (inputMode === "numeric") valueClass = "number";
  else if (type === "number") valueClass = step === "any" || isDecimalStep(step) ? "decimal" : "number";
  else if (["text", "email", "password", "search", "tel", "url"].includes(type)) valueClass = "string";
  if (dynamicNativeType) reasons.push(reason(span(node, source, filePath), "native-input-type", "type=dynamic-unresolved"));
  else if (type || inputMode || step) reasons.push(reason(span(node, source, filePath), kind, [type && `type=${type}`, inputMode && `inputMode=${inputMode}`, step && `step=${step}`].filter(Boolean).join(";")));
  else if (controlKind === "input") reasons.push(reason(span(node, source, filePath), "native-input-type", "type=default-text"));
  else if (controlKind === "select" || controlKind === "textarea") reasons.push(reason(span(node, source, filePath), "native-input-type", `element=${controlKind}`));
  else if (valueClass !== "unknown") reasons.push(reason(span(node, source, filePath), "label-context-clue", `component=${componentLeaf}`));
  if (valueClass === "unknown" && !dynamicNativeType) {
    const clueClass = clueValueClass(clue);
    if (clueClass !== "unknown") {
      valueClass = clueClass;
      reasons.push(reason(span(node, source, filePath), "label-context-clue", normalizedClue(clue)));
    }
  }
  return { valueClass, reasons };
}

function inferExpressionValueClass(expression: ts.Expression, source: ts.SourceFile, filePath: string, fieldName: string): { valueClass: ValueClass; reasons: ReasonEvidence[] } {
  const value = unwrap(expression);
  if (ts.isCallExpression(value)) {
    const name = unshadowedGlobalCastName(value, source);
    const castClass: ValueClass = name === "parseFloat" ? "decimal" : name === "parseInt" ? "integer" : name === "Number" ? "number"
      : name === "Boolean" ? "boolean" : name === "String" ? "string" : "unknown";
    if (castClass !== "unknown") return { valueClass: castClass, reasons: [reason(span(value, source, filePath), "parse-cast-function", name)] };
  }
  if (ts.isNumericLiteral(value)) {
    const valueClass: ValueClass = /[.eE]/u.test(value.getText(source)) ? "decimal" : "integer";
    return { valueClass, reasons: [reason(span(value, source, filePath), "submit-handler-propagation", "numeric-literal-class")] };
  }
  if (value.kind === ts.SyntaxKind.TrueKeyword || value.kind === ts.SyntaxKind.FalseKeyword) return { valueClass: "boolean", reasons: [reason(span(value, source, filePath), "submit-handler-propagation", "boolean-literal-class")] };
  if (ts.isStringLiteralLike(value) || ts.isTemplateExpression(value)) return { valueClass: "string", reasons: [reason(span(value, source, filePath), "submit-handler-propagation", "string-expression-class")] };
  if (ts.isArrayLiteralExpression(value)) return { valueClass: "array", reasons: [reason(span(value, source, filePath), "submit-handler-propagation", "array-literal-class")] };
  if (ts.isObjectLiteralExpression(value)) return { valueClass: "object", reasons: [reason(span(value, source, filePath), "submit-handler-propagation", "object-literal-class")] };
  const clueClass = clueValueClass(fieldName);
  return clueClass === "unknown" ? { valueClass: "unknown", reasons: [] }
    : { valueClass: clueClass, reasons: [reason(span(value, source, filePath), "label-context-clue", normalizedClue(fieldName))] };
}

function jsxAttributes(node: ts.JsxOpeningLikeElement): Map<string, ts.JsxAttribute> {
  const result = new Map<string, ts.JsxAttribute>();
  for (const property of node.attributes.properties) if (ts.isJsxAttribute(property) && ts.isIdentifier(property.name)) result.set(property.name.text, property);
  return result;
}

function literalAttribute(attribute: ts.JsxAttribute | undefined): string | null {
  if (!attribute) return null;
  if (!attribute.initializer) return "true";
  if (ts.isStringLiteral(attribute.initializer)) return attribute.initializer.text;
  if (!ts.isJsxExpression(attribute.initializer)) return null;
  const expression = attribute.initializer.expression;
  if (!expression) return null;
  if (ts.isStringLiteralLike(expression) || ts.isNumericLiteral(expression)) return expression.text;
  if (expression.kind === ts.SyntaxKind.TrueKeyword) return "true";
  if (expression.kind === ts.SyntaxKind.FalseKeyword) return "false";
  if (ts.isPrefixUnaryExpression(expression) && expression.operator === ts.SyntaxKind.MinusToken && ts.isNumericLiteral(expression.operand)) return `-${expression.operand.text}`;
  return null;
}

function expressionAttribute(attribute: ts.JsxAttribute | undefined): ts.Expression | null {
  const expression = attribute?.initializer && ts.isJsxExpression(attribute.initializer) ? attribute.initializer.expression : undefined;
  return expression ?? null;
}

function setterFieldBinding(attribute: ts.JsxAttribute | undefined, source: ts.SourceFile): string | null {
  const expression = attribute?.initializer && ts.isJsxExpression(attribute.initializer) ? attribute.initializer.expression : undefined;
  if (!expression) return null;
  const names: string[] = [];
  const collectObjectFields = (object: ts.ObjectLiteralExpression): void => {
    for (const property of object.properties) {
      if (!ts.isPropertyAssignment(property)) continue;
      const name = staticPropertyName(property.name);
      if (name) names.push(name);
    }
  };
  const visit = (node: ts.Node): void => {
    if (ts.isCallExpression(node)) {
      const callee = unwrap(node.expression);
      const calleeName = ts.isIdentifier(callee)
        ? callee.text
        : ts.isPropertyAccessExpression(callee) ? callee.name.text : "";
      if (/^set[A-Z_]/u.test(calleeName)) {
        for (const argument of node.arguments) {
          const value = unwrap(argument);
          if (ts.isObjectLiteralExpression(value)) collectObjectFields(value);
        }
      }
    }
    ts.forEachChild(node, visit);
  };
  visit(expression);
  const unique = [...new Set(names)];
  return unique.length === 1 ? unique[0] : null;
}

function selectOptionValues(node: ts.JsxOpeningLikeElement, source: ts.SourceFile): string[] {
  const element = node.parent;
  if (!ts.isJsxElement(element)) return [];
  const values: string[] = [];
  const visit = (candidate: ts.Node): void => {
    if (ts.isJsxOpeningElement(candidate) || ts.isJsxSelfClosingElement(candidate)) {
      const value = literalAttribute(jsxAttributes(candidate).get("value"));
      const leaf = candidate.tagName.getText(source).split(".").at(-1)?.toLowerCase() ?? "";
      if ((leaf === "option" || leaf === "selectitem" || leaf === "selectoption") && value !== null && value.length <= 256) values.push(value);
    }
    ts.forEachChild(candidate, visit);
  };
  for (const child of element.children) visit(child);
  return [...new Set(values)].sort().slice(0, maxRepresentativeValues);
}

function validationEvidence(attributes: Map<string, ts.JsxAttribute>): Record<string, string | boolean> {
  const result: Record<string, string | boolean> = {};
  for (const name of validationProps) {
    const value = literalAttribute(attributes.get(name));
    if (value !== null && value.length <= 256) result[name] = name === "required" ? value === "true" : value;
  }
  return Object.fromEntries(Object.entries(result).sort(([left], [right]) => left.localeCompare(right)));
}

function unshadowedGlobalCastName(call: ts.CallExpression, source: ts.SourceFile): string {
  const callee = unwrap(call.expression);
  if (!ts.isIdentifier(callee) || !["parseFloat", "parseInt", "Number", "Boolean", "String"].includes(callee.text)) return "";
  return hasVisibleDeclarationBefore(callee.text, callee, source) ? "" : callee.text;
}

function expressionBindingPath(expression: ts.Expression, source: ts.SourceFile): string {
  const value = unwrap(expression);
  if (ts.isIdentifier(value)) return value.text;
  if (ts.isPropertyAccessExpression(value)) {
    const owner = expressionBindingPath(value.expression, source);
    return owner ? `${owner}.${value.name.text}` : "";
  }
  if (ts.isElementAccessExpression(value) && value.argumentExpression && ts.isStringLiteralLike(value.argumentExpression)) {
    const owner = expressionBindingPath(value.expression, source);
    return owner ? `${owner}.${value.argumentExpression.text}` : "";
  }
  if (ts.isCallExpression(value) && value.arguments[0] && isBindingTransparentCall(value, source)) return expressionBindingPath(value.arguments[0], source);
  return "";
}

function expressionBindingRootIdentity(expression: ts.Expression, source: ts.SourceFile): string {
  const value = unwrap(expression);
  if (ts.isIdentifier(value)) return bindingRootIdentity(value, source);
  if (ts.isPropertyAccessExpression(value) || ts.isElementAccessExpression(value)) return expressionBindingRootIdentity(value.expression, source);
  if (ts.isCallExpression(value) && value.arguments[0] && isBindingTransparentCall(value, source)) return expressionBindingRootIdentity(value.arguments[0], source);
  return "";
}

function sameBindingRoot(left: string, right: string): boolean {
  if (left.startsWith("unresolved:") || right.startsWith("unresolved:")) return false;
  return Boolean(left && right && left === right);
}

function isBindingTransparentCall(call: ts.CallExpression, source: ts.SourceFile): boolean {
  const callee = unwrap(call.expression);
  return ts.isIdentifier(callee) && ["parseFloat", "parseInt", "Number", "Boolean", "String"].includes(callee.text)
    && !hasVisibleDeclarationBefore(callee.text, callee, source);
}

function resolveConstDeclaration(identifier: ts.Identifier, source: ts.SourceFile, scope: ts.Node): ts.VariableDeclaration | null {
  let resolved: ts.VariableDeclaration | null = null;
  const visit = (node: ts.Node): void => {
    if (node.getStart(source) >= identifier.getStart(source)) return;
    if (node !== scope && ts.isFunctionLike(node) && !isAncestor(node, identifier)) return;
    if (ts.isVariableDeclaration(node) && ts.isIdentifier(node.name) && node.name.text === identifier.text
      && ts.isVariableDeclarationList(node.parent) && (node.parent.flags & ts.NodeFlags.Const) !== 0
      && isAncestor(lexicalScope(node), identifier)) resolved = node;
    ts.forEachChild(node, visit);
  };
  visit(scope);
  return resolved;
}

function componentScope(node: ts.Node): ts.Node {
  let fallback: ts.Node = node.getSourceFile();
  for (let current: ts.Node | undefined = node.parent; current; current = current.parent) {
    if (!isFunctionLikeDeclaration(current)) continue;
    fallback = current;
    if (isComponentLikeFunction(current)) return current;
  }
  return fallback;
}

function isFunctionLikeDeclaration(node: ts.Node): node is ts.FunctionLikeDeclaration {
  return ts.isFunctionDeclaration(node)
    || ts.isFunctionExpression(node)
    || ts.isArrowFunction(node)
    || ts.isMethodDeclaration(node)
    || ts.isGetAccessorDeclaration(node)
    || ts.isSetAccessorDeclaration(node)
    || ts.isConstructorDeclaration(node);
}

function isComponentLikeFunction(node: ts.FunctionLikeDeclaration): boolean {
  const name = functionLikeName(node);
  if (name && /^[A-Z]/u.test(name)) return true;
  let returnsJsx = false;
  const visit = (candidate: ts.Node): void => {
    if (returnsJsx) return;
    if (candidate !== node && ts.isFunctionLike(candidate)) return;
    if (ts.isJsxElement(candidate) || ts.isJsxSelfClosingElement(candidate) || ts.isJsxFragment(candidate)) {
      returnsJsx = true;
      return;
    }
    ts.forEachChild(candidate, visit);
  };
  visit(node);
  return returnsJsx;
}

function functionLikeName(node: ts.FunctionLikeDeclaration): string {
  if (node.name && ts.isIdentifier(node.name)) return node.name.text;
  const parent = node.parent;
  if (ts.isVariableDeclaration(parent) && ts.isIdentifier(parent.name)) return parent.name.text;
  if (ts.isPropertyAssignment(parent) && ts.isIdentifier(parent.name)) return parent.name.text;
  return "";
}

function lexicalScope(node: ts.Node): ts.Node {
  for (let current: ts.Node | undefined = node.parent; current; current = current.parent) {
    if (ts.isBlock(current) || ts.isSourceFile(current) || ts.isCaseBlock(current)) return current;
  }
  return node.getSourceFile();
}

function hasVisibleDeclarationBefore(name: string, identifier: ts.Identifier, source: ts.SourceFile): boolean {
  return Boolean(resolveVisibleDeclaration(name, identifier, source));
}

function bindingRootIdentity(identifier: ts.Identifier, source: ts.SourceFile): string {
  const declaration = resolveVisibleDeclaration(identifier.text, identifier, source);
  return declaration
    ? `${identifier.text}:${declaration.getSourceFile().fileName}:${declaration.getStart(source)}:${declaration.getEnd()}`
    : `unresolved:${identifier.text}`;
}

function resolveVisibleDeclaration(name: string, identifier: ts.Identifier, source: ts.SourceFile): ts.Node | null {
  const containingScope = lexicalScope(identifier);
  let found: ts.Node | null = null;
  const visit = (node: ts.Node): void => {
    if (node === identifier || node.getStart(source) >= identifier.getStart(source)) return;
    const declarationName = declarationIdentifier(node);
    if (declarationName?.text === name && isAncestor(lexicalScope(node), identifier)) {
      found = declarationName;
      return;
    }
    if (node !== containingScope && ts.isFunctionLike(node) && !isAncestor(node, identifier)) return;
    ts.forEachChild(node, visit);
  };
  visit(source);
  return found;
}

function declarationIdentifier(node: ts.Node): ts.Identifier | null {
  if ((ts.isVariableDeclaration(node) || ts.isParameter(node) || ts.isFunctionDeclaration(node) || ts.isClassDeclaration(node)
    || ts.isImportSpecifier(node) || ts.isImportClause(node)) && node.name && ts.isIdentifier(node.name)) return node.name;
  if (ts.isBindingElement(node) && ts.isIdentifier(node.name)) return node.name;
  return null;
}

function hasBindingMutationBetween(name: string, declaration: ts.VariableDeclaration, identifier: ts.Identifier, source: ts.SourceFile, scope: ts.Node): boolean {
  let mutated = false;
  const declarationEnd = declaration.getEnd();
  const identifierStart = identifier.getStart(source);
  const visit = (node: ts.Node): void => {
    if (mutated) return;
    const start = node.getStart(source);
    if (start <= declarationEnd || start >= identifierStart) {
      ts.forEachChild(node, visit);
      return;
    }
    if (node !== scope && ts.isFunctionLike(node) && !isAncestor(node, identifier)) return;
    if (ts.isBinaryExpression(node) && isAssignmentOperator(node.operatorToken.kind) && bindingTargetRoot(node.left) === name) {
      mutated = true;
      return;
    }
    if ((ts.isPrefixUnaryExpression(node) || ts.isPostfixUnaryExpression(node))
      && [ts.SyntaxKind.PlusPlusToken, ts.SyntaxKind.MinusMinusToken].includes(node.operator)
      && bindingTargetRoot(node.operand) === name) {
      mutated = true;
      return;
    }
    ts.forEachChild(node, visit);
  };
  visit(scope);
  return mutated;
}

function isAssignmentOperator(kind: ts.SyntaxKind): boolean {
  return kind >= ts.SyntaxKind.FirstAssignment && kind <= ts.SyntaxKind.LastAssignment;
}

function bindingTargetRoot(expression: ts.Expression): string {
  const value = unwrap(expression);
  if (ts.isIdentifier(value)) return value.text;
  if (ts.isPropertyAccessExpression(value) || ts.isElementAccessExpression(value)) return bindingTargetRoot(value.expression);
  return "";
}

function isAncestor(ancestor: ts.Node, descendant: ts.Node): boolean {
  for (let current: ts.Node | undefined = descendant; current; current = current.parent) {
    if (current === ancestor) return true;
  }
  return false;
}

function staticPropertyName(name: ts.PropertyName): string | null {
  if (ts.isIdentifier(name) || ts.isStringLiteralLike(name) || ts.isNumericLiteral(name)) return name.text;
  return ts.isComputedPropertyName(name) && ts.isStringLiteralLike(name.expression) ? name.expression.text : null;
}

function clueValueClass(value: string): ValueClass {
  const normalized = value.replace(/([a-z])([A-Z])/g, "$1_$2").toLowerCase();
  if (decimalClues.test(normalized)) return "decimal";
  if (numberClues.test(normalized)) return "number";
  return "unknown";
}

function normalizedClue(value: string): string {
  const match = value.replace(/([a-z])([A-Z])/g, "$1_$2").toLowerCase().match(decimalClues) ?? value.toLowerCase().match(numberClues);
  return match?.[1] ?? "numeric-context";
}

function isDecimalStep(value: string): boolean {
  return /^\d*\.\d+$/u.test(value) && Number(value) > 0;
}

function mergeValueClasses(left: ValueClass, right: ValueClass): ValueClass {
  if (left === "unknown") return right;
  if (right === "unknown" || left === right) return left;
  if ([left, right].includes("decimal") && ["integer", "number", "decimal"].includes(left) && ["integer", "number", "decimal"].includes(right)) return "decimal";
  if (["integer", "number", "decimal"].includes(left) && ["integer", "number", "decimal"].includes(right)) return "number";
  return "unknown";
}

function mergeSubmittedValueClass(control: ValueClass, submitted: ValueClass): ValueClass {
  if (["integer", "number", "decimal"].includes(submitted) && ["integer", "number", "decimal"].includes(control)) {
    return mergeValueClasses(control, submitted);
  }
  return submitted;
}

function normalizePayloadBindings(bindings: PayloadBinding[]): PayloadBinding[] {
  const result = new Map<string, PayloadBinding>();
  for (const binding of bindings) {
    const key = [binding.operationEvidenceId, binding.fieldName, binding.bindingPath, binding.bindingRootIdentity, binding.evidence.startOffset, binding.evidence.endOffset].join("\0");
    result.set(key, binding);
  }
  return [...result.values()].sort((left, right) => left.evidence.startOffset - right.evidence.startOffset
    || left.entityName.localeCompare(right.entityName) || left.fieldName.localeCompare(right.fieldName));
}

function normalizeReasons(reasons: ReasonEvidence[]): ReasonEvidence[] {
  const byKey = new Map(reasons.map((item) => [JSON.stringify(item), item]));
  return [...byKey.values()].sort((left, right) => left.filePath.localeCompare(right.filePath)
    || left.startOffset - right.startOffset || left.kind.localeCompare(right.kind) || left.detail.localeCompare(right.detail));
}

function reason(sourceSpan: SourceSpan, kind: ReasonEvidence["kind"], detail: string): ReasonEvidence {
  return { ...sourceSpan, ruleId: RuleIds.Base44UiInputSemantics, kind, detail };
}

function span(node: ts.Node, source: ts.SourceFile, filePath: string): SourceSpan {
  return {
    filePath,
    startLine: source.getLineAndCharacterOfPosition(node.getStart(source)).line + 1,
    endLine: source.getLineAndCharacterOfPosition(node.getEnd()).line + 1,
    startOffset: node.getStart(source),
    endOffset: node.getEnd(),
    snippetSha256: hash(node.getText(source), 64)
  };
}

function uiFact(manifest: ScanManifest, source: ts.SourceFile, sourceText: string, evidence: SourceSpan, contract: UiSemanticsContract): CodeFact {
  const target = contract.submittedEntity && contract.submittedField
    ? `${contract.submittedEntity}.${contract.submittedField}`
    : contract.fieldBinding || `${contract.componentName}@${evidence.startLine}`;
  return createFact(manifest, FactTypes.Base44UiInputSemantics, RuleIds.Base44UiInputSemantics,
    EvidenceTiers.Tier3SyntaxOrTextual,
    createEvidence(evidence.filePath, evidence.startLine, evidence.endLine, "base44-evidence", ScannerVersions.Base44EvidenceExtractor, evidence.snippetSha256), {
      targetSymbol: contract.submittedEntity || null,
      contractElement: target,
      properties: {
        sourceFileSha256: hash(sourceText, 64),
        uiSemanticsJson: JSON.stringify(contract)
      }
    });
}

function unwrap(expression: ts.Expression): ts.Expression {
  let current = expression;
  while (ts.isParenthesizedExpression(current) || ts.isAsExpression(current) || ts.isTypeAssertionExpression(current)
    || ts.isNonNullExpression(current) || ts.isSatisfiesExpression(current)) current = current.expression;
  return current;
}
