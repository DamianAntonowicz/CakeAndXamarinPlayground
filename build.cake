#addin "Cake.Xamarin"
#tool "nuget:?package=GitVersion.CommandLine&version=5.0.1" // Reference older version because newest doesn't work on macOS.

var target = Argument("target", (string)null);

//====================================================================
// Consts

// General
const string PATH_TO_SOLUTION = "TastyFormsApp.sln";
const string PATH_TO_UNIT_TESTS_PROJECT = "TastyFormsApp.Tests/TastyFormsApp.Tests.csproj";
const string APP_PACKAGE_FOLDER_NAME = "AppPackages";

// Android
const string PATH_TO_ANDROID_PROJECT = "TastyFormsApp.Android/TastyFormsApp.Android.csproj";

// iOS
const string PATH_TO_IOS_PROJECT = "TastyFormsApp.iOS/TastyFormsApp.iOS.csproj";

//====================================================================
// Moves app package to app packages folder

public string MoveAppPackageToPackagesFolder(FilePath appPackageFilePath)
{
    var packageFileName = appPackageFilePath.GetFilename();
    var targetAppPackageFilePath = new FilePath($"{APP_PACKAGE_FOLDER_NAME}/" + packageFileName);

    if (FileExists(targetAppPackageFilePath))
    {
        DeleteFile(targetAppPackageFilePath);
    }

    EnsureDirectoryExists($"{APP_PACKAGE_FOLDER_NAME}");
    MoveFile(appPackageFilePath, targetAppPackageFilePath);

    return targetAppPackageFilePath.ToString();
}

//====================================================================
// Class that hold information for current build.

public class BuildInfo
{
    public string ApiUrl { get; }

    public BuildInfo(string apiUrl)
    {
        ApiUrl = apiUrl;
    }
}

//====================================================================
// Setups script.

Setup<BuildInfo>(setupContext => 
{
    var gitVersion = GitVersion();
    var branchName = gitVersion.BranchName;

    string apiUrl = "https://dev.tastyformsapp.com";

    if (branchName.StartsWith("release/"))
    {
        apiUrl = "https://staging.tastyformsapp.com";
    }
    else if (branchName.Equals("master"))
    {
        apiUrl = "https://prod.tastyformsapp.com";
    }

    return new BuildInfo(apiUrl);
});

//====================================================================
// Cleans all bin and obj folders.

Task("Clean")
  .Does(() =>
{
  CleanDirectories("**/bin");
  CleanDirectories("**/obj");
});

//====================================================================
// Restores NuGet packages for solution.

Task("Restore")
  .Does(() =>
{
  NuGetRestore(PATH_TO_SOLUTION);
});

//====================================================================
// Updates config files with proper values

Task("UpdateConfigFiles")
  .Does<BuildInfo>(BuildInfo =>
  {
      var appSettingsFile = File("TastyFormsApp/AppSettings.cs");
      TransformTextFile(appSettingsFile)
        .WithToken("API_URL", BuildInfo.ApiUrl)
        .Save(appSettingsFile);
  });

//====================================================================
// Run unit tests

Task("RunUnitTests")
  .IsDependentOn("Clean")
  .IsDependentOn("Restore")
  .Does(() =>
  {
     var settings = new DotNetCoreTestSettings
     {
         Configuration = "Release"
     };

      DotNetCoreTest(PATH_TO_UNIT_TESTS_PROJECT, settings);
  });

//====================================================================
// Publish Android APK

Task("PublishAPK")
  .IsDependentOn("RunUnitTests")
  .IsDependentOn("UpdateConfigFiles")
  .Does(() => 
{
    var apkFilePath = BuildAndroidApk(PATH_TO_ANDROID_PROJECT, sign: true);
    MoveAppPackageToPackagesFolder(apkFilePath);
});

//====================================================================

Task("PublishIPA")
  .IsDependentOn("RunUnitTests")
  .IsDependentOn("UpdateConfigFiles")
  .Does(() =>
  {
    var ipaFilePath = BuildiOSIpa(PATH_TO_IOS_PROJECT, "Release");
    MoveAppPackageToPackagesFolder(ipaFilePath);
  });

RunTarget(target);