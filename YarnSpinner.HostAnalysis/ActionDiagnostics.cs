using Microsoft.CodeAnalysis;

public static class ActionDiagnostics
{
    // public static readonly DiagnosticDescriptor YSDIAGNOSTICTEMPLATE = new(
    //     "CODE",
    //     title: "TITLE",
    //     messageFormat: "FORMATMESSAGE",
    //     category: "Yarn Spinner",
    //     defaultSeverity: DiagnosticSeverity.Error,
    //     isEnabledByDefault: true
    // );

    public static readonly DiagnosticDescriptor YS1000InternalErrorProcessingAction = new(
        "YS1000",
        title: "An internal error was encountered while processing actions",
        messageFormat: "An internal error was encountered while processing actions and converters: {0}",
        category: "Yarn Spinner",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true
    );

    public static readonly DiagnosticDescriptor YS1001ActionMethodsMustBePublic = new(
        "YS1001",
        title: "Yarn action methods must be public",
        messageFormat: "Attributed YarnCommand and YarnFunction methods must be public. \"{0}\" is \"{1}\".",
        category: "Yarn Spinner",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true
    );

    public static readonly DiagnosticDescriptor YS1002ActionMethodsMustHaveAValidName = new(
        "YS1002",
        title: "Yarn action methods must have a valid name",
        messageFormat: "YarnCommand and YarnFunction methods must follow existing ID rules for Yarn. \"{0}\" is invalid.",
        category: "Yarn Spinner",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true
    );

    public static readonly DiagnosticDescriptor YS1003CommandMethodsMustHaveAValidReturnType = new(
        "YS1003",
        title: "YarnCommand methods must return a valid type",
        messageFormat: "YarnCommand methods must return a valid type (either void, a coroutine, or a task). \"{0}\"'s return type is \"{1}\".",
        category: "Yarn Spinner",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true
    );

    public static readonly DiagnosticDescriptor YS1004FunctionMethodsMustHaveAValidReturnType = new(
        "YS1004",
        title: "YarnFunction methods must return a valid type",
        messageFormat: "YarnFunction methods must return a valid type—either bool, string, or a numeric type—or a task type that returns one of these types. \"{0}\"'s return type is \"{1}\".",
        category: "Yarn Spinner",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true
    );

    public static readonly DiagnosticDescriptor YS1005ActionsParamsArraysMustBeOfYarnTypes = new(
        "YS1005",
        title: "Parameter arrays must be of a Yarn compatible type",
        messageFormat: "Parameter arrays must be of a Yarn compatible type, but \"{0}\" is of type \"{1}\"",
        category: "Yarn Spinner",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true
    );

    public static readonly DiagnosticDescriptor YS1006CancellationTokenInWrongLocation = new(
        "YS1006",
        title: "Yarn actions can accept cancellation tokens but they must be the last parameter in the action",
        messageFormat: "Yarn actions can accept cancellation tokens but they must be the last parameter in the action but {0} is in position {1}. Other pieces rely on being able to assume the last parameter is the token, and this is also a general recommendation for C# methods.",
        category: "Yarn Spinner",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true
    );

    public static readonly DiagnosticDescriptor YS1007ArrayInWrongLocation = new(
        "YS1007",
        title: "Yarn actions can accept parameter arrays but they must be the last parameter in the action",
        messageFormat: "Yarn actions can accept parameter arrays but they must be the last parameter in the action but {0} is in position {1}. Other pieces rely on being able to assume the last parameter is the parameter array.",
        category: "Yarn Spinner",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true
    );

    public static readonly DiagnosticDescriptor YS1008ActionsParameterIsAnIncompatibleType = new(
        "YS1008",
        title: "Yarn action parameters must be of a Yarn compatible type",
        messageFormat: "Yarn action parameters must be of a Yarn compatible type. They can be booleans, numbers, strings, Game Objects, Component subclasses, or any type with a Converter method. \"{0}\" is \"{1}\".",
        category: "Yarn Spinner",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        customTags: WellKnownDiagnosticTags.CompilationEnd
    );

    public static readonly DiagnosticDescriptor YS1009InstanceActionIsOnAnIncompatibleType = new(
        "YS1009",
        title: "Instance action must on a type that is convertible and accessible",
        messageFormat: "Instance action must on a type that is convertible and accessible. {0} is {1}.",
        category: "Yarn Spinner",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        customTags: WellKnownDiagnosticTags.CompilationEnd
    );

    public static readonly DiagnosticDescriptor YS1010ParameterIsAnOut = new(
        "YS1010",
        title: "Action parameter is an out parameter",
        messageFormat: "Action parameter {0} is an out parameter. Out parameters are unable to be supported in Yarn Spinner as there is no way to make use of the value.",
        category: "Yarn Spinner",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true
    );

    public static readonly DiagnosticDescriptor YS1011ConverterMethodIsNotStatic = new(
        "YS1011",
        title: "Converter method isn't static",
        messageFormat: "Converter method {0} is not static. Converter methods are required to be static so they can be called as part of action invocation.",
        category: "Yarn Spinner",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true
    );

    public static readonly DiagnosticDescriptor YS1012ConverterMethodIsNotPublic = new(
        "YS1012",
        title: "Converter method isn't public",
        messageFormat: "Converter method {0} is not public. Converter methods are required to be accessible so they can be called as part of action invocation.",
        category: "Yarn Spinner",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true
    );

    public static readonly DiagnosticDescriptor YS1013ConverterReturnsInvalidType = new(
        "YS1013",
        title: "Converter method returns invalid type",
        messageFormat: "Converter method {0} returns a {1}. Converters are required to return bool.",
        category: "Yarn Spinner",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true
    );

    public static readonly DiagnosticDescriptor YS1014IncorrectNumberOfConverterParameters = new(
        "YS1014",
        title: "Incorrect number of parameters for a converter method",
        messageFormat: "Converter methods require exactly two parameters. {0} has {1}.",
        category: "Yarn Spinner",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true
    );

    public static readonly DiagnosticDescriptor YS1015ConverterHasInvalidInputParameter = new(
        "YS1015",
        title: "Converters first parameter is not a string",
        messageFormat: "Converter methods require the first parameter to be a String. This is needed as the input from Yarn to let you uniquely convert to the requested type. {0} is {1}.",
        category: "Yarn Spinner",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true
    );

    public static readonly DiagnosticDescriptor YS1016ConverterMissingOutParam = new(
        "YS1016",
        title: "Converters second parameter is not an out parameter",
        messageFormat: "Converter methods require the second parameter to be an out parameter",
        category: "Yarn Spinner",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true
    );

    public static readonly DiagnosticDescriptor YS1017ConverterTypeMismatch = new(
        "YS1017",
        title: "Converter type mismatch",
        messageFormat: "Converter methods require the out parameter type and the attribute type to match. The attribute type {0} does not match the out parameter type {1}.",
        category: "Yarn Spinner",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true
    );

    public static readonly DiagnosticDescriptor YS1018DuplicateConverter = new(
        "YS1018",
        title: "Duplicate converter",
        messageFormat: "{0} defines conversion support for {1} but an existing converter already exists for this type. Only one converter method will be called and it will be arbitrary as to which one will be chosen.",
        category: "Yarn Spinner",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        customTags: WellKnownDiagnosticTags.CompilationEnd
    );

    public static readonly DiagnosticDescriptor YS1019DuplicateAction = new(
        "YS1019",
        title: "Duplicate action",
        messageFormat: "{0} has been registered as a {1} multiple times. Only one {1} will be called and it will be arbitrary as to which will be chosen.",
        category: "Yarn Spinner",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        customTags: WellKnownDiagnosticTags.CompilationEnd
    );

    public static readonly DiagnosticDescriptor YS1020UnableToResolveActionName = new(
        "YS1020",
        title: "Unable to resolve actions name",
        messageFormat: "Unable to resolve the name of the action, either the value is not a constant string or isn't a string at all. This value needs to be known at compile time.",
        category: "Yarn Spinner",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true
    );

    public static readonly DiagnosticDescriptor YS1021ActionIsALambda = new(
        "YS1021",
        title: "Action is a lambda",
        messageFormat: "Yarn actions can be lambdas but this generally isn't recommended. Lambda based actions cannot be unregistered, are more difficult to debug and can't be directly invoked.",
        category: "Yarn Spinner",
        defaultSeverity: DiagnosticSeverity.Info,
        isEnabledByDefault: true
    );

    public static readonly DiagnosticDescriptor YS1022ActionsEnumAttributedParameterIsOfIncompatibleType = new(
        "YS1022",
        title: "Parameter is attributed as an enum but is of an invalid type",
        messageFormat: "Parameter is attributed as an enum but is of an invalid type. Enums must be of a basic type but {0} is a '{1}'.",
        category: "Yarn Spinner",
        defaultSeverity: DiagnosticSeverity.Info,
        isEnabledByDefault: true
    );

    public static readonly DiagnosticDescriptor YS1023ActionsNodeAttributedParameterIsOfIncompatibleType = new(
        "YS1023",
        title: "Yarn Node attributed parameters must be a string",
        messageFormat: "Yarn Node attributed parameters must be a string, but {0} is a '{1}'",
        category: "Yarn Spinner",
        defaultSeverity: DiagnosticSeverity.Info,
        isEnabledByDefault: true
    );

    public static readonly DiagnosticDescriptor YS1024ActionIsALocalFunction = new(
        "YS1024",
        title: "Action is a local function",
        messageFormat: "Yarn actions can be local functions but this generally isn't recommended. Local functions are implicitly capturing the object performing the registration, but non-instance actions in Yarn are global, so accidentally having two (or more) objects with this registration in the scene will lead to undefined behaviour.",
        category: "Yarn Spinner",
        defaultSeverity: DiagnosticSeverity.Info,
        isEnabledByDefault: true
    );

    public static readonly DiagnosticDescriptor YS1025DirectActionIsPrivate = new(
        "YS1025",
        title: "Direct registered Action is private",
        messageFormat: "Yarn actions can be directly registered as a private method but this generally isn't recommended. This will be creating a delegate from the captured method and invoking that via reflection as there is no other way to call into a private method.",
        category: "Yarn Spinner",
        defaultSeverity: DiagnosticSeverity.Info,
        isEnabledByDefault: true
    );

    public static readonly DiagnosticDescriptor YS1026FunctionUsesMetaToken = new(
        "YS1026",
        title: "Function uses a LineCancellationToken",
        messageFormat: "Making your actions use LineCancellationTokens are normally a good idea however due to the nature of how function invocation works there is no way to hurry up the function. Functions are always invoked by Yarn Spinner before their value is used so there is no concept of the current content to ask to hurry up.",
        category: "Yarn Spinner",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true
    );
}