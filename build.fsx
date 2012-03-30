#r "./packages/FAKE.1.64.5/tools/FakeLib.dll"

open Fake 
open System.IO

// properties
let currentDate = System.DateTime.UtcNow
let projectName = "FSharpx"
let version = if isLocalBuild then "1.4." + currentDate.ToString("yMMdd") else buildVersion
let coreSummary = "FSharpx is a library for the .NET platform implementing general functional constructs on top of the F# core library."
let projectSummary = "FSharpx is a library for the .NET platform implementing general functional constructs on top of the F# core library."
let projectDescription = "FSharpx is a library for the .NET platform implementing general functional constructs on top of the F# core library. Its main target is F# but it aims to be compatible with all .NET languages wherever possible.\r\n\r\nIt currently implements:\r\n\r\n* Several standard monads: State, Reader, Writer, Either, Continuation, Distribution\r\n* Iteratee\r\n* Validation applicative functor\r\n* General functions like flip\r\n* Additional functions around collections\r\n* Functions to make C# - F# interop easier."
let httpDesc = "This library provides common features for working with HTTP applications."
let asyncDesc = "This library implements various extensions for asynchronous programming using F# asynchronous workflows and F# agents."
let observableDesc = "This library implements a mini-Reactive Extensions (MiniRx) and was authored by Phil Trelford."
let typeProvidersDesc = "This library is for the .NET platform implementing common type providers on top of the FSharpx.Core."
let authors = ["Steffen Forkmann"; "Daniel Mohl"; "Tomas Petricek"; "Ryan Riley"; "Mauricio Scheffer"; "Phil Trelford" ]
let mail = "ryan.riley@panesofglass.org"
let homepage = "http://github.com/fsharp/fsharpx"

// directories
let buildDir = "./build/"
let packagesDir = "./packages/"
let testDir = "./test/"
let deployDir = "./deploy/"
let docsDir = "./docs/"

let targetPlatformDir = getTargetPlatformDir "4.0.30319"

let nugetDir package = sprintf "./nuget/%s/" package
let nugetLibDir package = nugetDir package @@ "lib"
let nugetDocsDir package = nugetDir package @@ "docs"

let nugetDirHttp = "./nuget/Http/"
let nugetLibDirHttp = nugetDirHttp @@ "lib"
let nugetDocsDirHttp = nugetDirHttp @@ "docs"

let net35 = "v3.5"
let net40 = "v4.0"
let net45 = "v4.5"

// params
let target = getBuildParamOrDefault "target" "All"
let buildSpecific = hasBuildParam "v35" || hasBuildParam "v40" || hasBuildParam "v45"

let normalizeFrameworkVersion frameworkVersion =
    let v = ("[^\\d]" >=> "") frameworkVersion
    v.Substring(0,2)

let frameworkParams frameworkVersion = ["TargetFrameworkVersion", frameworkVersion; "DefineConstants", "NET" + normalizeFrameworkVersion frameworkVersion]

// tools
let fakeVersion = GetPackageVersion packagesDir "FAKE"
let fakePath = sprintf "%sFAKE.%s/tools" packagesDir fakeVersion
let nugetPath = "./lib/Nuget/nuget.exe"
let nunitVersion = GetPackageVersion packagesDir "NUnit"
let nunitPath = sprintf "%sNUnit.%s/Tools" packagesDir nunitVersion

// files
let appReferences frameworkVersion =    
    { (!+ "./src/**/*.*proj") with 
        Excludes = 
            [yield "./src/**/*.Silverlight.*proj"
             if frameworkVersion <> net45 then yield "./src/**/*.TypeProviders.*proj"
             if frameworkVersion = net35 then 
                yield "./src/**/*.Async.fsproj"
                yield "./src/**/*.Http.fsproj" // TODO: why is that?
                yield "./src/**/*.Observable.fsproj" // TODO: why is that?
                  ] }
    |> Scan

let testReferences frameworkVersion =
    { (!+ "./tests/**/*.*proj") with 
        Excludes = [if frameworkVersion <> net45 then yield "./tests/**/*.TypeProviders.*proj"] }
    |> Scan

// targets
Target "Clean" (fun _ ->
    CleanDirs [buildDir; testDir; deployDir; docsDir; nugetDir "Core"; nugetLibDir "Core"; nugetDocsDir "Core"; nugetDir "Http"; nugetLibDir "Http"; nugetDocsDir "Http"]
)

Target "AssemblyInfo" (fun _ ->
    AssemblyInfo (fun p ->
        {p with 
            CodeLanguage = FSharp
            AssemblyVersion = version
            AssemblyTitle = projectName
            AssemblyDescription = projectSummary
            Guid = "1e95a279-c2a9-498b-bc72-6e7a0d6854ce"
            OutputFileName = "./src/FSharpx.Core/AssemblyInfo.fs" })

    AssemblyInfo (fun p ->
        {p with 
            CodeLanguage = FSharp
            AssemblyVersion = version
            AssemblyTitle = "FSharpx.Http"
            AssemblyDescription = httpDesc
            Guid = "60F3BB81-5449-45DD-A217-B6045327680C"
            OutputFileName = "./src/FSharpx.Http/AssemblyInfo.fs" })

    AssemblyInfo (fun p ->
        {p with 
            CodeLanguage = FSharp
            AssemblyVersion = version
            AssemblyTitle = "FSharpx.Async"
            AssemblyDescription = asyncDesc
            Guid = "ede1812b-5a62-410a-9553-02499cf29317"
            OutputFileName = "./src/FSharpx.Async/AssemblyInfo.fs" })

    AssemblyInfo (fun p ->
        {p with 
            CodeLanguage = FSharp
            AssemblyVersion = version
            AssemblyTitle = "FSharpx.Observable"
            AssemblyDescription = observableDesc
            Guid = "2E802F54-9CD0-4B0A-B834-5C5979403B50"
            OutputFileName = "./src/FSharpx.Observable/AssemblyInfo.fs" })

    AssemblyInfo (fun p ->
        {p with 
            CodeLanguage = FSharp
            AssemblyVersion = version
            AssemblyTitle = "FSharpx.TypeProviders"
            AssemblyDescription = typeProvidersDesc
            Guid = "89B6AF94-507D-4BE0-98FA-A5124884DBA8"
            OutputFileName = "./src/FSharpx.TypeProviders/AssemblyInfo.fs" })
)

let buildAppTarget = TargetTemplate (fun frameworkVersion ->
    CleanDir buildDir

    appReferences frameworkVersion
    |> MSBuild buildDir "Build" (["Configuration","Release"] @ frameworkParams frameworkVersion)
    |> Log "AppBuild-Output: "
)

let buildTestTarget = TargetTemplate (fun frameworkVersion ->
    CleanDir testDir
    testReferences frameworkVersion
    |> MSBuild testDir "Build" ["Configuration","Debug"] 
    |> Log "TestBuild-Output: "
)

let testTarget = TargetTemplate (fun frameworkVersion ->
    ActivateFinalTarget "CloseTestRunner"
    !! (testDir + "/*.Tests.dll")
    |> NUnit (fun p ->
        {p with
            ToolPath = nunitPath
            DisableShadowCopy = true
            OutputFile = testDir + sprintf "TestResults.%s.xml" frameworkVersion })
)

Target "GenerateDocumentation" (fun _ ->
    !! (buildDir + "*.dll")
    |> Docu (fun p ->
        {p with
            ToolPath = fakePath + "/docu.exe"
            TemplatesPath = "./lib/templates"
            OutputPath = docsDir })
)

Target "CopyLicense" (fun _ ->
    [ "LICENSE.md" ] |> CopyTo buildDir
)

Target "ZipDocumentation" (fun _ ->
    !! (docsDir + "/**/*.*")
    |> Zip docsDir (deployDir + sprintf "Documentation-%s.zip" version)
)

let prepareNugetTarget = TargetTemplate (fun frameworkVersion ->
    let frameworkSubDir package = nugetLibDir package @@ normalizeFrameworkVersion frameworkVersion
    CleanDirs [frameworkSubDir "Core";frameworkSubDir "Http"]

    let getFiles libs = 
        [ for lib in libs do
          for ending in ["dll";"pdb";"xml"] ->
            sprintf "%s%s.%s" buildDir lib ending ]
    
    [yield "FSharpx.Core"
     if frameworkVersion <> net35  then 
        yield "FSharpx.Observable"
        yield "FSharpx.Async"]
    |> getFiles    
    |> CopyTo (frameworkSubDir "Core")

    if frameworkVersion <> net35  then
        ["FSharpx.Http"]
        |> getFiles    
        |> CopyTo (frameworkSubDir "Http")
)

let buildFrameworkVersionTarget = TargetTemplate (fun frameworkVersion -> ())

let generateTargets() =
    [if hasBuildParam "v35" then yield net35
     if (hasBuildParam "v40") || (not buildSpecific) then yield net40
     if hasBuildParam "v45" then yield net45]
    |> Seq.fold
        (fun dependency frameworkVersion -> 
            tracefn "Generating targets for .NET %s" frameworkVersion
            let v = normalizeFrameworkVersion frameworkVersion
            let buildApp = sprintf "BuildApp_%s" v
            let buildTest = sprintf "BuildTest_%s" v
            let test = sprintf "Test_%s" v
            let prepareNuget = sprintf "PrepareNuget_%s" v
            let buildFrameworkVersion = sprintf "Build_%s" v

            buildAppTarget buildApp frameworkVersion
            buildTestTarget buildTest frameworkVersion
            testTarget test frameworkVersion
            prepareNugetTarget prepareNuget frameworkVersion
            buildFrameworkVersionTarget buildFrameworkVersion frameworkVersion

            dependency ==> buildApp ==> buildTest ==> test ==> prepareNuget ==> buildFrameworkVersion)
            "CopyLicense"

Target "BuildNuGet.Core" (fun _ ->
    XCopy (docsDir |> FullName) (nugetDocsDir "Core")
    NuGet (fun p -> 
        {p with               
            Authors = authors
            Project = projectName + ".Core"
            Description = projectDescription
            Version = version
            OutputPath = nugetDir "Core"
            ToolPath = nugetPath
            AccessKey = getBuildParamOrDefault "nugetkey" ""
            Publish = hasBuildParam "nugetkey" })
        "FSharpx.Core.nuspec"

    !! (nugetDir "Core" + "FSharpx.Core.*.nupkg")
      |> CopyTo deployDir
)

Target "BuildNuGet.Http" (fun _ ->
    NuGet (fun p -> 
        {p with               
            Authors = authors
            Project = projectName + ".Http"
            Description = httpDesc
            Version = version
            OutputPath = nugetDir "Http"
            ToolPath = nugetPath
            Dependencies =
                [projectName + ".Core",RequireExactly (NormalizeVersion version)]
            AccessKey = getBuildParamOrDefault "nugetkey" ""
            Publish = hasBuildParam "nugetkey" })
        "FSharpx.Core.nuspec"

    !! (nugetDir "Http" + "FSharpx.Http.*.nupkg")
      |> CopyTo deployDir
)

Target "DeployZip" (fun _ ->
    !! (buildDir + "/**/*.*")
    |> Zip buildDir (deployDir + sprintf "%s-%s.zip" projectName version)
)

FinalTarget "CloseTestRunner" (fun _ ->  
    ProcessHelper.killProcess "nunit-agent.exe"
)

Target "Deploy" DoNothing
Target "All" DoNothing

// Build order
"Clean"
  ==> "AssemblyInfo"
  ==> "CopyLicense"
  ==> (generateTargets())
  ==> "GenerateDocumentation"
  ==> "ZipDocumentation"
  ==> "BuildNuGet.Core"
  ==> "BuildNuGet.Http"
  ==> "DeployZip"
  ==> "Deploy"

"All" <== ["Deploy"]

// Start build
Run target