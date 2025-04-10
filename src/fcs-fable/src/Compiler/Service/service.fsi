// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

// SourceCodeServices API to the compiler as an incremental service for parsing,
// type checking and intellisense-like environment-reporting.
namespace FSharp.Compiler.CodeAnalysis

open System
open System.IO
open System.Threading
open System.Threading.Tasks
open FSharp.Compiler.AbstractIL.ILBinaryReader
open FSharp.Compiler.CodeAnalysis
#if !FABLE_COMPILER
open FSharp.Compiler.CodeAnalysis.TransparentCompiler
#endif
open FSharp.Compiler.CompilerConfig
open FSharp.Compiler.Diagnostics
open FSharp.Compiler.EditorServices
open FSharp.Compiler.Symbols
open FSharp.Compiler.Syntax
open FSharp.Compiler.Text
open FSharp.Compiler.Tokenization

#if !FABLE_COMPILER

/// Used to parse and check F# source code.
[<Sealed; AutoSerializable(false)>]
type public FSharpChecker =
    /// <summary>
    /// Create an instance of an FSharpChecker.
    /// </summary>
    ///
    /// <param name="projectCacheSize">The optional size of the project checking cache.</param>
    /// <param name="keepAssemblyContents">Keep the checked contents of projects.</param>
    /// <param name="keepAllBackgroundResolutions">If false, do not keep full intermediate checking results from background checking suitable for returning from GetBackgroundCheckResultsForFileInProject. This reduces memory usage.</param>
    /// <param name="legacyReferenceResolver">An optional resolver for legacy MSBuild references</param>
    /// <param name="tryGetMetadataSnapshot">An optional resolver to access the contents of .NET binaries in a memory-efficient way</param>
    /// <param name="suggestNamesForErrors">Indicate whether name suggestion should be enabled</param>
    /// <param name="keepAllBackgroundSymbolUses">Indicate whether all symbol uses should be kept in background checking</param>
    /// <param name="enableBackgroundItemKeyStoreAndSemanticClassification">Indicates whether a table of symbol keys should be kept for background compilation</param>
    /// <param name="enablePartialTypeChecking">Indicates whether to perform partial type checking. Cannot be set to true if keepAssemblyContents is true. If set to true, can cause duplicate type-checks when richer information on a file is needed, but can skip background type-checking entirely on implementation files with signature files.</param>
    /// <param name="parallelReferenceResolution">Indicates whether to resolve references in parallel.</param>
    /// <param name="captureIdentifiersWhenParsing">When set to true we create a set of all identifiers for each parsed file which can be used to speed up finding references.</param>
    /// <param name="documentSource">Default: FileSystem. You can use Custom source to provide a function that will return the source for a given file path instead of reading it from the file system. Note that with this option the FSharpChecker will also not monitor the file system for file changes. It will expect to be notified of changes via the NotifyFileChanged method.</param>
    /// <param name="useTransparentCompiler">Default: false. Indicates whether we use a new experimental background compiler. This does not yet support all features</param>
    /// <param name="transparentCompilerCacheSizes">Default: None. The cache sizes for the transparent compiler</param>
    static member Create:
        ?projectCacheSize: int *
        ?keepAssemblyContents: bool *
        ?keepAllBackgroundResolutions: bool *
        ?legacyReferenceResolver: LegacyReferenceResolver *
        ?tryGetMetadataSnapshot: ILReaderTryGetMetadataSnapshot *
        ?suggestNamesForErrors: bool *
        ?keepAllBackgroundSymbolUses: bool *
        ?enableBackgroundItemKeyStoreAndSemanticClassification: bool *
        ?enablePartialTypeChecking: bool *
        ?parallelReferenceResolution: bool *
        ?captureIdentifiersWhenParsing: bool *
        [<Experimental "This parameter is experimental and likely to be removed in the future.">] ?documentSource:
            DocumentSource *
        [<Experimental "This parameter is experimental and likely to be removed in the future.">] ?useTransparentCompiler:
            bool *
        [<Experimental "This parameter is experimental and likely to be removed in the future.">] ?transparentCompilerCacheSizes:
            CacheSizes ->
            FSharpChecker

    [<Experimental("This FCS API is experimental and subject to change.")>]
    member UsesTransparentCompiler: bool

    /// <summary>
    ///   Parse a source code file, returning information about brace matching in the file.
    ///   Return an enumeration of the matching parenthetical tokens in the file.
    /// </summary>
    ///
    /// <param name="fileName">The fileName for the file, used to help caching of results.</param>
    /// <param name="sourceText">The full source for the file.</param>
    /// <param name="options">Parsing options for the project or script.</param>
    /// <param name="userOpName">An optional string used for tracing compiler operations associated with this request.</param>
    member MatchBraces:
        fileName: string * sourceText: ISourceText * options: FSharpParsingOptions * ?userOpName: string ->
            Async<(range * range)[]>

    /// <summary>
    ///   Parse a source code file, returning information about brace matching in the file.
    ///   Return an enumeration of the matching parenthetical tokens in the file.
    /// </summary>
    ///
    /// <param name="fileName">The fileName for the file, used to help caching of results.</param>
    /// <param name="source">The full source for the file.</param>
    /// <param name="options">Parsing options for the project or script.</param>
    /// <param name="userOpName">An optional string used for tracing compiler operations associated with this request.</param>
    [<Obsolete("Please pass FSharpParsingOptions to MatchBraces. If necessary generate FSharpParsingOptions from FSharpProjectOptions by calling checker.GetParsingOptionsFromProjectOptions(options)")>]
    member MatchBraces:
        fileName: string * source: string * options: FSharpProjectOptions * ?userOpName: string ->
            Async<(range * range)[]>

    /// <summary>
    /// Parses a source code for a file and caches the results. Returns an AST that can be traversed for various features.
    /// </summary>
    ///
    /// <param name="fileName">The path for the file. The file name is used as a module name for implicit top level modules (e.g. in scripts).</param>
    /// <param name="sourceText">The source to be parsed.</param>
    /// <param name="options">Parsing options for the project or script.</param>
    /// <param name="cache">Store the parse in a size-limited cache associated with the FSharpChecker. Default: true</param>
    /// <param name="userOpName">An optional string used for tracing compiler operations associated with this request.</param>
    member ParseFile:
        fileName: string * sourceText: ISourceText * options: FSharpParsingOptions * ?cache: bool * ?userOpName: string ->
            Async<FSharpParseFileResults>

    [<Experimental("This FCS API is experimental and subject to change.")>]
    member ParseFile:
        fileName: string * projectSnapshot: FSharpProjectSnapshot * ?userOpName: string -> Async<FSharpParseFileResults>

    /// <summary>
    /// Parses a source code for a file. Returns an AST that can be traversed for various features.
    /// </summary>
    ///
    /// <param name="fileName">The path for the file. The file name is also as a module name for implicit top level modules (e.g. in scripts).</param>
    /// <param name="source">The source to be parsed.</param>
    /// <param name="options">Parsing options for the project or script.</param>
    /// <param name="cache">Store the parse in a size-limited cache associated with the FSharpChecker. Default: true</param>
    /// <param name="userOpName">An optional string used for tracing compiler operations associated with this request.</param>
    [<Obsolete("Please call checker.ParseFile instead.  To do this, you must also pass FSharpParsingOptions instead of FSharpProjectOptions. If necessary generate FSharpParsingOptions from FSharpProjectOptions by calling checker.GetParsingOptionsFromProjectOptions(options)")>]
    member ParseFileInProject:
        fileName: string * source: string * options: FSharpProjectOptions * ?cache: bool * ?userOpName: string ->
            Async<FSharpParseFileResults>

    /// <summary>
    /// <para>Check a source code file, returning a handle to the results of the parse including
    /// the reconstructed types in the file.</para>
    ///
    /// <para>All files except the one being checked are read from the FileSystem API</para>
    /// <para>Note: returns NoAntecedent if the background builder is not yet done preparing the type check context for the
    /// file (e.g. loading references and parsing/checking files in the project that this file depends upon).
    /// In this case, the caller can either retry, or wait for FileTypeCheckStateIsDirty to be raised for this file.
    /// </para>
    /// </summary>
    ///
    /// <param name="parseResults">The results of ParseFile for this file.</param>
    /// <param name="fileName">The name of the file in the project whose source is being checked.</param>
    /// <param name="fileVersion">An integer that can be used to indicate the version of the file. This will be returned by TryGetRecentCheckResultsForFile when looking up the file.</param>
    /// <param name="source">The full source for the file.</param>
    /// <param name="options">The options for the project or script.</param>
    /// <param name="userOpName">An optional string used for tracing compiler operations associated with this request.</param>
    [<Obsolete("This member should no longer be used, please use 'CheckFileInProject'")>]
    member CheckFileInProjectAllowingStaleCachedResults:
        parseResults: FSharpParseFileResults *
        fileName: string *
        fileVersion: int *
        source: string *
        options: FSharpProjectOptions *
        ?userOpName: string ->
            Async<FSharpCheckFileAnswer option>

    /// <summary>
    /// <para>
    ///   Check a source code file, returning a handle to the results
    /// </para>
    /// <para>
    ///    Note: all files except the one being checked are read from the FileSystem API
    /// </para>
    /// <para>
    ///   Return FSharpCheckFileAnswer.Aborted if a parse tree was not available.
    /// </para>
    /// </summary>
    ///
    /// <param name="parseResults">The results of ParseFile for this file.</param>
    /// <param name="fileName">The name of the file in the project whose source is being checked.</param>
    /// <param name="fileVersion">An integer that can be used to indicate the version of the file. This will be returned by TryGetRecentCheckResultsForFile when looking up the file.</param>
    /// <param name="sourceText">The full source for the file.</param>
    /// <param name="options">The options for the project or script.</param>
    /// <param name="userOpName">An optional string used for tracing compiler operations associated with this request.</param>
    member CheckFileInProject:
        parseResults: FSharpParseFileResults *
        fileName: string *
        fileVersion: int *
        sourceText: ISourceText *
        options: FSharpProjectOptions *
        ?userOpName: string ->
            Async<FSharpCheckFileAnswer>

    /// <summary>
    /// <para>
    ///   Parse and check a source code file, returning a handle to the results
    /// </para>
    /// <para>
    ///    Note: all files except the one being checked are read from the FileSystem API
    /// </para>
    /// <para>
    ///   Return FSharpCheckFileAnswer.Aborted if a parse tree was not available.
    /// </para>
    /// </summary>
    ///
    /// <param name="fileName">The name of the file in the project whose source is being checked.</param>
    /// <param name="fileVersion">An integer that can be used to indicate the version of the file. This will be returned by TryGetRecentCheckResultsForFile when looking up the file.</param>
    /// <param name="sourceText">The source for the file.</param>
    /// <param name="options">The options for the project or script.</param>
    /// <param name="userOpName">An optional string used for tracing compiler operations associated with this request.</param>
    member ParseAndCheckFileInProject:
        fileName: string *
        fileVersion: int *
        sourceText: ISourceText *
        options: FSharpProjectOptions *
        ?userOpName: string ->
            Async<FSharpParseFileResults * FSharpCheckFileAnswer>

    [<Experimental("This FCS API is experimental and subject to change.")>]
    member ParseAndCheckFileInProject:
        fileName: string * projectSnapshot: FSharpProjectSnapshot * ?userOpName: string ->
            Async<FSharpParseFileResults * FSharpCheckFileAnswer>

    /// <summary>
    /// <para>Parse and typecheck all files in a project.</para>
    /// <para>All files are read from the FileSystem API</para>
    /// <para>Can cause a second type-check on the entire project when `enablePartialTypeChecking` is true on the FSharpChecker.</para>
    /// </summary>
    ///
    /// <param name="options">The options for the project or script.</param>
    /// <param name="userOpName">An optional string used for tracing compiler operations associated with this request.</param>
    member ParseAndCheckProject: options: FSharpProjectOptions * ?userOpName: string -> Async<FSharpCheckProjectResults>

    [<Experimental("This FCS API is experimental and subject to change.")>]
    member ParseAndCheckProject:
        projectSnapshot: FSharpProjectSnapshot * ?userOpName: string -> Async<FSharpCheckProjectResults>

    /// <summary>
    /// <para>For a given script file, get the FSharpProjectOptions implied by the #load closure.</para>
    /// <para>All files are read from the FileSystem API, except the file being checked.</para>
    /// </summary>
    ///
    /// <param name="fileName">Used to differentiate between scripts, to consider each script a separate project. Also used in formatted error messages.</param>
    /// <param name="source">The source for the file.</param>
    /// <param name="previewEnabled">Is the preview compiler enabled.</param>
    /// <param name="loadedTimeStamp">Indicates when the script was loaded into the editing environment,
    /// so that an 'unload' and 'reload' action will cause the script to be considered as a new project,
    /// so that references are re-resolved.</param>
    /// <param name="otherFlags">Other flags for compilation.</param>
    /// <param name="useFsiAuxLib">Add a default reference to the FSharp.Compiler.Interactive.Settings library.</param>
    /// <param name="useSdkRefs">Use the implicit references from the .NET SDK.</param>
    /// <param name="assumeDotNetFramework">Set up compilation and analysis for .NET Framework scripts.</param>
    /// <param name="sdkDirOverride">Override the .NET SDK used for default references.</param>
    /// <param name="optionsStamp">An optional unique stamp for the options.</param>
    /// <param name="userOpName">An optional string used for tracing compiler operations associated with this request.</param>
    member GetProjectOptionsFromScript:
        fileName: string *
        source: ISourceText *
        ?previewEnabled: bool *
        ?loadedTimeStamp: DateTime *
        ?otherFlags: string[] *
        ?useFsiAuxLib: bool *
        ?useSdkRefs: bool *
        ?assumeDotNetFramework: bool *
        ?sdkDirOverride: string *
        ?optionsStamp: int64 *
        ?userOpName: string ->
            Async<FSharpProjectOptions * FSharpDiagnostic list>

    /// <param name="fileName">Used to differentiate between scripts, to consider each script a separate project. Also used in formatted error messages.</param>
    /// <param name="source">The source for the file.</param>
    /// <param name="documentSource">DocumentSource to load any additional files.</param>
    /// <param name="previewEnabled">Is the preview compiler enabled.</param>
    /// <param name="loadedTimeStamp">Indicates when the script was loaded into the editing environment,
    /// so that an 'unload' and 'reload' action will cause the script to be considered as a new project,
    /// so that references are re-resolved.</param>
    /// <param name="otherFlags">Other flags for compilation.</param>
    /// <param name="useFsiAuxLib">Add a default reference to the FSharp.Compiler.Interactive.Settings library.</param>
    /// <param name="useSdkRefs">Use the implicit references from the .NET SDK.</param>
    /// <param name="assumeDotNetFramework">Set up compilation and analysis for .NET Framework scripts.</param>
    /// <param name="sdkDirOverride">Override the .NET SDK used for default references.</param>
    /// <param name="optionsStamp">An optional unique stamp for the options.</param>
    /// <param name="userOpName">An optional string used for tracing compiler operations associated with this request.</param>
    [<Experimental("This FCS API is experimental and subject to change.")>]
    member GetProjectSnapshotFromScript:
        fileName: string *
        source: ISourceTextNew *
        ?documentSource: DocumentSource *
        ?previewEnabled: bool *
        ?loadedTimeStamp: DateTime *
        ?otherFlags: string[] *
        ?useFsiAuxLib: bool *
        ?useSdkRefs: bool *
        ?assumeDotNetFramework: bool *
        ?sdkDirOverride: string *
        ?optionsStamp: int64 *
        ?userOpName: string ->
            Async<FSharpProjectSnapshot * FSharpDiagnostic list>

    /// <summary>Get the FSharpProjectOptions implied by a set of command line arguments.</summary>
    ///
    /// <param name="projectFileName">Used to differentiate between projects and for the base directory of the project.</param>
    /// <param name="argv">The command line arguments for the project build.</param>
    /// <param name="loadedTimeStamp">Indicates when the script was loaded into the editing environment,
    /// <param name="isEditing">Indicates that compilation should assume the EDITING define and related settings</param>
    /// <param name="isInteractive">Indicates that compilation should assume the INTERACTIVE define and related settings</param>
    /// so that an 'unload' and 'reload' action will cause the script to be considered as a new project,
    /// so that references are re-resolved.</param>
    member GetProjectOptionsFromCommandLineArgs:
        projectFileName: string * argv: string[] * ?loadedTimeStamp: DateTime * ?isInteractive: bool * ?isEditing: bool ->
            FSharpProjectOptions

    /// <summary>
    /// <para>Get the FSharpParsingOptions implied by a set of command line arguments and list of source files.</para>
    /// </summary>
    ///
    /// <param name="sourceFiles">Initial source files list. Additional files may be added during argv evaluation.</param>
    /// <param name="argv">The command line arguments for the project build.</param>
    /// <param name="isInteractive">Indicates that parsing should assume the INTERACTIVE define and related settings</param>
    /// <param name="isEditing">Indicates that compilation should assume the EDITING define and related settings</param>
    member GetParsingOptionsFromCommandLineArgs:
        sourceFiles: string list * argv: string list * ?isInteractive: bool * ?isEditing: bool ->
            FSharpParsingOptions * FSharpDiagnostic list

    /// <summary>
    /// <para>Get the FSharpParsingOptions implied by a set of command line arguments.</para>
    /// </summary>
    ///
    /// <param name="argv">The command line arguments for the project build.</param>
    /// <param name="isInteractive">Indicates that parsing should assume the INTERACTIVE define and related settings</param>
    /// <param name="isEditing">Indicates that compilation should assume the EDITING define and related settings</param>
    member GetParsingOptionsFromCommandLineArgs:
        argv: string list * ?isInteractive: bool * ?isEditing: bool -> FSharpParsingOptions * FSharpDiagnostic list

    /// <summary>
    /// <para>Get the FSharpParsingOptions implied by a FSharpProjectOptions.</para>
    /// </summary>
    ///
    /// <param name="options">The overall options.</param>
    member GetParsingOptionsFromProjectOptions:
        options: FSharpProjectOptions -> FSharpParsingOptions * FSharpDiagnostic list

    /// <summary>
    /// <para>Like ParseFile, but uses results from the background builder.</para>
    /// <para>All files are read from the FileSystem API, including the file being checked.</para>
    /// </summary>
    ///
    /// <param name="fileName">The name for the file.</param>
    /// <param name="options">The options for the project or script, used to determine active --define conditionals and other options relevant to parsing.</param>
    /// <param name="userOpName">An optional string used for tracing compiler operations associated with this request.</param>
    member GetBackgroundParseResultsForFileInProject:
        fileName: string * options: FSharpProjectOptions * ?userOpName: string -> Async<FSharpParseFileResults>

    /// <summary>
    /// <para>Like CheckFileInProject, but uses the existing results from the background builder.</para>
    /// <para>All files are read from the FileSystem API, including the file being checked.</para>
    /// <para>Can cause a second type-check when `enablePartialTypeChecking` is true on the FSharpChecker.</para>
    /// </summary>
    ///
    /// <param name="fileName">The file name for the file.</param>
    /// <param name="options">The options for the project or script, used to determine active --define conditionals and other options relevant to parsing.</param>
    /// <param name="userOpName">An optional string used for tracing compiler operations associated with this request.</param>
    member GetBackgroundCheckResultsForFileInProject:
        fileName: string * options: FSharpProjectOptions * ?userOpName: string ->
            Async<FSharpParseFileResults * FSharpCheckFileResults>

    /// <summary>
    /// <para>Optimized find references for a given symbol in a file of project.</para>
    /// <para>All files are read from the FileSystem API, including the file being checked.</para>
    /// <para>Can cause a second type-check when `enablePartialTypeChecking` is true on the FSharpChecker.</para>
    /// </summary>
    ///
    /// <param name="fileName">The file name for the file.</param>
    /// <param name="options">The options for the project or script, used to determine active --define conditionals and other options relevant to parsing.</param>
    /// <param name="symbol">The symbol to find all uses in the file.</param>
    /// <param name="canInvalidateProject">Default: true. If true, this call can invalidate the current state of project if the options have changed. If false, the current state of the project will be used.</param>
    /// <param name="fastCheck">Default: false. Experimental feature that makes the operation faster. Requires FSharpChecker to be created with captureIdentifiersWhenParsing = true.</param>
    /// <param name="userOpName">An optional string used for tracing compiler operations associated with this request.</param>
    member FindBackgroundReferencesInFile:
        fileName: string *
        options: FSharpProjectOptions *
        symbol: FSharpSymbol *
        ?canInvalidateProject: bool *
        [<Experimental("This FCS API is experimental and subject to change.")>] ?fastCheck: bool *
        ?userOpName: string ->
            Async<range seq>

    [<Experimental("This FCS API is experimental and subject to change.")>]
    member FindBackgroundReferencesInFile:
        fileName: string * projectSnapshot: FSharpProjectSnapshot * symbol: FSharpSymbol * ?userOpName: string ->
            Async<range seq>

    /// <summary>
    /// <para>Get semantic classification for a file.</para>
    /// <para>All files are read from the FileSystem API, including the file being checked.</para>
    /// <para>Can cause a second type-check when `enablePartialTypeChecking` is true on the FSharpChecker.</para>
    /// </summary>
    ///
    /// <param name="fileName">The file name for the file.</param>
    /// <param name="options">The options for the project or script, used to determine active --define conditionals and other options relevant to parsing.</param>
    /// <param name="userOpName">An optional string used for tracing compiler operations associated with this request.</param>
    member GetBackgroundSemanticClassificationForFile:
        fileName: string * options: FSharpProjectOptions * ?userOpName: string ->
            Async<SemanticClassificationView option>

    /// <summary>
    /// <para>Get semantic classification for a file.</para>
    /// </summary>
    ///
    /// <param name="fileName">The file name for the file.</param>
    /// <param name="snapshot">The project snapshot for which we want to get the semantic classification.</param>
    /// <param name="userOpName">An optional string used for tracing compiler operations associated with this request.</param>
    [<Experimental("This FCS API is experimental and subject to change.")>]
    member GetBackgroundSemanticClassificationForFile:
        fileName: string * snapshot: FSharpProjectSnapshot * ?userOpName: string ->
            Async<SemanticClassificationView option>

    /// <summary>
    /// Compile using the given flags.  Source files names are resolved via the FileSystem API.
    /// The output file must be given by a -o flag.
    /// The first argument is ignored and can just be "fsc.exe".
    /// The method returns the collected diagnostics, and (possibly) a terminating exception.
    /// </summary>
    ///
    /// <param name="argv">The command line arguments for the project build.</param>
    /// <param name="userOpName">An optional string used for tracing compiler operations associated with this request.</param>
    member Compile: argv: string[] * ?userOpName: string -> Async<FSharpDiagnostic[] * exn option>

    /// <summary>
    /// Try to get type check results for a file. This looks up the results of recent type checks of the
    /// same file, regardless of contents. The version tag specified in the original check of the file is returned.
    /// If the source of the file has changed the results returned by this function may be out of date, though may
    /// still be usable for generating intellisense menus and information.
    /// </summary>
    ///
    /// <param name="fileName">The file name for the file.</param>
    /// <param name="options">The options for the project or script, used to determine active --define conditionals and other options relevant to parsing.</param>
    /// <param name="sourceText">Optionally, specify source that must match the previous parse precisely.</param>
    /// <param name="userOpName">An optional string used for tracing compiler operations associated with this request.</param>
    member TryGetRecentCheckResultsForFile:
        fileName: string * options: FSharpProjectOptions * ?sourceText: ISourceText * ?userOpName: string ->
            (FSharpParseFileResults * FSharpCheckFileResults (* hash *) * int64) option

    [<Experimental("This FCS API is experimental and subject to change.")>]
    member TryGetRecentCheckResultsForFile:
        fileName: string * projectSnapshot: FSharpProjectSnapshot * ?userOpName: string ->
            (FSharpParseFileResults * FSharpCheckFileResults) option

    /// This function is called when the entire environment is known to have changed for reasons not encoded in the ProjectOptions of any project/compilation.
    member InvalidateAll: unit -> unit

    /// <summary>
    ///  This function is called when the configuration is known to have changed for reasons not encoded in the ProjectOptions.
    ///  For example, dependent references may have been deleted or created.
    /// </summary>
    /// <param name="options">The options for the project or script, used to determine active --define conditionals and other options relevant to parsing.</param>
    /// <param name="userOpName">An optional string used for tracing compiler operations associated with this request.</param>
    member InvalidateConfiguration: options: FSharpProjectOptions * ?userOpName: string -> unit

    /// <summary>
    ///  This function is called when the configuration is known to have changed for reasons not encoded in the projectSnapshot.
    ///  For example, dependent references may have been deleted or created.
    /// </summary>
    [<Experimental("This FCS API is experimental and subject to change.")>]
    member InvalidateConfiguration: projectSnapshot: FSharpProjectSnapshot * ?userOpName: string -> unit

    /// <summary>Clear the internal cache of the given projects.</summary>
    /// <param name="options">The given project options.</param>
    /// <param name="userOpName">An optional string used for tracing compiler operations associated with this request.</param>
    member ClearCache: options: FSharpProjectOptions seq * ?userOpName: string -> unit

    member ClearCache: projects: ProjectSnapshot.FSharpProjectIdentifier seq * ?userOpName: string -> unit

    /// Report a statistic for testability
    static member ActualParseFileCount: int

    /// Report a statistic for testability
    static member ActualCheckFileCount: int

    /// Flush all caches and garbage collect
    member ClearLanguageServiceRootCachesAndCollectAndFinalizeAllTransients: unit -> unit

    /// Notify the checker that given file has changed. This needs to be used when checker is created with documentSource = Custom.
    [<Experimental "This FCS API is experimental and likely to be removed in the future.">]
    member NotifyFileChanged: fileName: string * options: FSharpProjectOptions * ?userOpName: string -> Async<unit>

    /// <summary>
    /// This function is called when a project has been cleaned/rebuilt, and thus any live type providers should be refreshed.
    /// </summary>
    ///
    /// <param name="options">The options describing the project that has been cleaned.</param>
    /// <param name="userOpName">An optional string used for tracing compiler operations associated with this request.</param>
    [<Obsolete("This method is obsolete and will be removed in a future release")>]
    member NotifyProjectCleaned: options: FSharpProjectOptions * ?userOpName: string -> Async<unit>

    /// <summary>
    /// Notify the host that the logical type checking context for a file has now been updated internally
    /// and that the file has become eligible to be re-typechecked for errors.
    /// The event will be raised on a background thread.
    /// </summary>
    member BeforeBackgroundFileCheck: IEvent<string * FSharpProjectOptions>

    /// Raised after a parse of a file in the background analysis.
    ///
    /// The event will be raised on a background thread.
    member FileParsed: IEvent<string * FSharpProjectOptions>

    /// Raised after a check of a file in the background analysis.
    ///
    /// The event will be raised on a background thread.
    member FileChecked: IEvent<string * FSharpProjectOptions>

    /// Notify the host that a project has been fully checked in the background (using file contents provided by the file system API)
    ///
    /// The event may be raised on a background thread.
    member ProjectChecked: IEvent<FSharpProjectOptions>

    member internal TransparentCompiler: TransparentCompiler

    member internal Caches: CompilerCaches

    [<Obsolete("Please create an instance of FSharpChecker using FSharpChecker.Create")>]
    static member Instance: FSharpChecker

    member internal FrameworkImportsCache: FrameworkImportsCache
    member internal ReferenceResolver: LegacyReferenceResolver

    /// Tokenize a single line, returning token information and a tokenization state represented by an integer
    member TokenizeLine: line: string * state: FSharpTokenizerLexState -> FSharpTokenInfo[] * FSharpTokenizerLexState

    /// Tokenize an entire file, line by line
    member TokenizeFile: source: string -> FSharpTokenInfo[][]

namespace FSharp.Compiler

open System
open FSharp.Compiler.CodeAnalysis

/// Information about the compilation environment
[<Class>]
type public CompilerEnvironment =
    /// The default location of FSharp.Core.dll and fsc.exe based on the version of fsc.exe that is running
    static member BinFolderOfDefaultFSharpCompiler: ?probePoint: string -> string option

    /// These are the names of assemblies that should be referenced for .fs or .fsi files that
    /// are not associated with a project.
    static member DefaultReferencesForOrphanSources: assumeDotNetFramework: bool -> string list

    /// Return the compilation defines that should be used when editing the given file.
    static member GetConditionalDefinesForEditing: parsingOptions: FSharpParsingOptions -> string list

    /// Return true if this is a subcategory of error or warning message that the language service can emit
    static member IsCheckerSupportedSubcategory: string -> bool

    /// Return the language ID, which is the expression evaluator id that the debugger will use.
    static member GetDebuggerLanguageID: unit -> Guid

    /// A helpers for dealing with F# files.
    static member IsScriptFile: string -> bool

    /// Whether or not this file is compilable
    static member IsCompilable: string -> bool

    /// Whether or not this file should be a single-file project
    static member MustBeSingleFileProject: string -> bool

#endif //!FABLE_COMPILER
