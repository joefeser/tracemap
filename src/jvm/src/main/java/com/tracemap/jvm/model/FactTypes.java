package com.tracemap.jvm.model;

public final class FactTypes {
    private FactTypes() {
    }

    public static final String REPO_SCANNED = "RepoScanned";
    public static final String BUILD_STATUS = "BuildStatus";
    public static final String ANALYSIS_GAP = "AnalysisGap";
    public static final String FILE_INVENTORIED = "FileInventoried";
    public static final String PROJECT_DECLARED = "ProjectDeclared";
    public static final String PACKAGE_REFERENCED = "PackageReferenced";
    public static final String CONFIG_FILE_DECLARED = "ConfigFileDeclared";
    public static final String SQL_FILE_DECLARED = "SqlFileDeclared";
    public static final String TYPE_DECLARED = "TypeDeclared";
    public static final String METHOD_DECLARED = "MethodDeclared";
    public static final String PROPERTY_DECLARED = "PropertyDeclared";
    public static final String FIELD_DECLARED = "FieldDeclared";
    public static final String PARAMETER_DECLARED = "ParameterDeclared";
    public static final String ATTRIBUTE_USED = "AttributeUsed";
    public static final String LOCAL_ALIAS = "LocalAlias";
    public static final String SYMBOL_RELATIONSHIP = "SymbolRelationship";
    public static final String MEMBER_ACCESS_NAME = "MemberAccessName";
    public static final String INVOCATION_NAME = "InvocationName";
    public static final String CALL_EDGE = "CallEdge";
    public static final String OBJECT_CREATED = "ObjectCreated";
    public static final String ARGUMENT_PASSED = "ArgumentPassed";
    public static final String CALCULATION_EXPRESSION = "CalculationExpression";
    public static final String BRANCHING_LOGIC = "BranchingLogic";
    public static final String RETRY_POLICY_LOGIC = "RetryPolicyLogic";
    public static final String SERIALIZATION_LOGIC = "SerializationLogic";
    public static final String INFRASTRUCTURE_BOILERPLATE = "InfrastructureBoilerplate";
    public static final String QUERY_PATTERN_DETECTED = "QueryPatternDetected";
    public static final String PROPERTY_ACCESSED = "PropertyAccessed";
    public static final String METHOD_INVOKED = "MethodInvoked";
    public static final String HTTP_CALL_DETECTED = "HttpCallDetected";
    public static final String SQL_TEXT_USED = "SqlTextUsed";
    public static final String CONFIG_KEY_DECLARED = "ConfigKeyDeclared";
    public static final String HTTP_ROUTE_BINDING = "HttpRouteBinding";
    public static final String DATABASE_COLUMN_MAPPING = "DatabaseColumnMapping";
    public static final String SERIALIZER_CONTRACT_MEMBER = "SerializerContractMember";
}
