"""Independent acceptance contract, transcribed from the documented authored matrices.

This input is maintained separately from native smoke implementation and output.
Do not regenerate it from a smoke receipt or import smoke EXPECTED/case_plan here.
Sources: SQLSERVER_PARAMETER_EVIDENCE.md, SQLSERVER_RESULT_COLUMN_EVIDENCE.md,
and the consumer-helpers / field-reads design matrices. Both languages use every cell.
"""
LANGUAGES = ('csharp', 'vb')
M, X, U = 'bounded-match', 'mismatch', 'unknown'
ORIGINS = ('result-schema', 'ddl-result', 'ddl-table-result')
REPORTS = {'helper': 'helper-result-column-comparison.json',
           'field': 'field-read-column-comparison.json'}


def cells(text):
    """Literal name:ordered-status-codes[:variant] rows, not observed results."""
    result = {}
    for cell in text.split():
        name, codes, *variant = cell.split(':')
        if name in result:
            raise ValueError('duplicate authored acceptance cell')
        result[name] = ([{'M': M, 'X': X, 'U': U}[c] for c in codes], variant[0] if variant else None)
    return result


PARAMETER = cells('''Good:M SizeMismatch:X TypeMismatch:X DirectionMismatch:X Max:M MissingSize:U
Ansi:U Decimal:U CaseName:U InputOutput:M Channels:U DynamicName:U Conditional:U Alias:U
ParameterAlias:U Mutation:U ParameterReassignment:U HelperBoundary:U Text:U DynamicCommand:U
Duplicate:U Extra:X Missing:X DefaultOmitted:U TwoCommands#0:X TwoCommands#1:M''')

READER = cells('''NameMatch:M MissingName:X Renamed:X OrdinalValid:M OrdinalOutOfRange:X
Reordered:MM GetOrdinalDirect:MM GetOrdinalLocal:MM GetOrdinalMissing:XX CaseOnly:U
DuplicateDeclared:U DynamicName:U DynamicOrdinal:U AbsentBinding:U ReaderAlias:U
ReaderReassignment:U CommandAlias:U OrdinalAlias:MU OrdinalReassignment:MU HelperEscape:U
TwoReaders#0:M TwoReaders#1:M ReadLoop:MM ReadGuard:M UsingLifetime:M NextResult:U ScalarChannel:U
NonQueryChannel:U RecordsAffected:U Incomplete:U TextCommand:U Conditional:U WithParameter:M NullCheck:M''')
READER_TEMPLATES = {'Renamed': 'renamed.sql', 'Reordered': 'reordered.sql',
                    'DuplicateDeclared': 'duplicate.sql', 'Incomplete': 'if.sql'}
READER_BODIES = ('if.sql', 'nested.sql', 'dynamic.sql', 'multiple.sql', 'star.sql', 'from.sql',
                 'union.sql', 'assignment.sql', 'settings.sql', 'return.sql', 'noalias.sql')

HELPER_CALLERS = cells('''TableName:M TableOrdinal:M TableForeach:M TableRowsGuard:M OtherProcedure:M
TwoCallers#0:M TwoCallers#1:M SetIndex:M SetDefaultName:M SetSourceName:M SetTableLocalGuards:M
TextHelper:M ConnectionHelper:M ConstProcedure:M MissingName:X OrdinalOutOfRange:X
OtherProcedureMissing:X SetMissing:X Renamed:X:renamed CaseOnly:U DynamicName:U AbsentBinding:U
Incomplete:U:incomplete''')
HELPER_UNKNOWN = '''PublicHelper InternalHelper InstanceHelper VirtualHelper GenericHelper OverloadedHelper#0
OverloadedHelper#1 ExtensionHelper PartialHelper RefHelper OptionalHelper TwoStringHelper ObjectHelper
RecursiveHelper DelegateHelper MutatingHelper GuardedHelper ExecutingHelper MultipleReturnHelper
ConstantTextHelper ParameterWriteHelper CallingHelper DisposedHelper UncalledHelper TextCommandHelper
InlineFill CallerRefill'''.split()
CALLER_UNKNOWN = '''CallerNamedArgument CallerDynamicProcedure CallerNullProcedure CallerDiscarded
CallerChained CallerPassed CallerObjectLocal CallerRowWrite CallerRefill CallerReassigned#0 CallerReassigned#1
CallerAlias CallerTryCatch CallerLambda CallerLaterTable CallerSetMutation CallerSourceNameMismatch
CallerConnectionArgument CallerNoReads'''.split()
BOUNDED_HELPERS = ('ReadTable', 'ReadSet', 'ReadNamedSet', 'ReadTableText', 'ReadTableUsing')

FIELD_FILLS = cells('''TableFieldName:M TableFieldOrdinal:M TableFieldRowLocal:M TableFieldForeach:M
TableFieldMixed:MM TableFieldMissing:X TableFieldOrdinalOutOfRange:X TableFieldCaseOnly:U TableFieldDynamic:U
TableFieldRenamed:X:renamed TableFieldIncomplete:U:incomplete SetFieldName:M SetFieldTableLocal:M SetFieldMissing:X''')
FIELD_SITES = cells('''HelperFieldName:M HelperFieldOtherProcedure:M HelperFieldMissing:X InternalFieldName:M
InternalFieldOrdinal:M InternalFieldSet:M InternalFieldMissing:X''')
FIELD_UNKNOWN = '''TableFieldColumn TableFieldVersion TableSetField TableFieldAsEnumerable TableFieldStatic
TableFieldUnbound TableNoField SetFieldLaterTable ReadBody'''.split()
FIELD_SITE_UNKNOWN = ('HelperFieldColumn', 'InternalFieldVersion', 'InternalNoField')
FIELD_HAS_UNKNOWN_SITE = ('TableFieldUnbound', 'TableNoField')
FIELD_HELPERS = {
    'ReadPrivate': ('HelperFieldName', 'HelperFieldOtherProcedure', 'HelperFieldMissing', 'HelperFieldColumn'),
    'ReadShared': ('InternalFieldName', 'InternalFieldOrdinal', 'InternalFieldMissing', 'InternalFieldVersion', 'InternalNoField'),
    'ReadSharedSet': ('InternalFieldSet',),
}


def plan(label):
    if label == 'helper':
        rows = [('caller', key, origin, variant, list(statuses))
                for key, (statuses, variant) in HELPER_CALLERS.items() for origin in ORIGINS]
        rows += [('helper', key, 'ddl-table-result', None, [U]) for key in HELPER_UNKNOWN]
        return rows + [('caller', key, 'ddl-table-result', None, [U]) for key in CALLER_UNKNOWN]
    if label == 'field':
        rows = [(kind, key, origin, variant, list(statuses))
                for kind, source in (('fill', FIELD_FILLS), ('site', FIELD_SITES))
                for key, (statuses, variant) in source.items() for origin in ORIGINS]
        rows += [('fill', key, 'ddl-table-result', None, [U]) for key in FIELD_UNKNOWN]
        return rows + [('site', key, 'ddl-table-result', None, [U]) for key in FIELD_SITE_UNKNOWN]
    raise ValueError('unknown authored acceptance route')
