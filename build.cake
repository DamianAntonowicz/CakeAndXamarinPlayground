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
const string PATH_TO_ANDROID_MANIFEST_FILE = "TastyFormsApp.Android/Properties/AndroidManifest.xml";

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
    public string BuildNumber { get; }
    public string AppVersion { get; }
    public string AppName { get; }
    public string PackageName { get; }

    public BuildInfo(
      string apiUrl, 
      string buildNumber, 
      string appVersion,
      string appName,
      string packageName)
    {
        ApiUrl = apiUrl;
        BuildNumber = buildNumber;
        AppVersion = appVersion;
        AppName = appName;
        PackageName = packageName;
    }
}

//====================================================================
// Setups script.

Setup<BuildInfo>(setupContext => 
{
    var gitVersion = GitVersion();
    var branchName = gitVersion.BranchName;
    var apiUrl = "https://dev.tastyformsapp.com";
    var appName = "TastyFormsApp.dev";
    var packageName = "com.tastyformsapp.dev";

    if (branchName.StartsWith("release/"))
    {
        apiUrl = "https://staging.tastyformsapp.com";
        appName = "TastyFormsApp.staging";
        packageName = "com.tastyformsapp.staging";
    }
    else if (branchName.Equals("master"))
    {
        apiUrl = "https://prod.tastyformsapp.com";
        appName = "TastyFormsApp";
        packageName = "com.tastyformsapp";
    }

    return new BuildInfo(
      apiUrl,
      buildNumber: DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(),
      appVersion: gitVersion.MajorMinorPatch,
      appName,
      packageName);
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
  .Does<BuildInfo>(buildInfo =>
  {
      var appSettingsFile = File("TastyFormsApp/AppSettings.cs");
      TransformTextFile(appSettingsFile)
        .WithToken("API_URL", buildInfo.ApiUrl)
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
  .Does<BuildInfo>(buildInfo => 
{
    var xmlPokeSettings = new XmlPokeSettings
    {
        Namespaces = new Dictionary<string, string>
        {
            {"android", "http://schemas.android.com/apk/res/android"}
        }
    };

    var androidManifest = PATH_TO_ANDROID_MANIFEST_FILE;
    XmlPoke(androidManifest, "/manifest/@android:versionName", buildInfo.AppVersion, xmlPokeSettings);
    XmlPoke(androidManifest, "/manifest/@android:versionCode", buildInfo.BuildNumber, xmlPokeSettings);
    XmlPoke(androidManifest, "/manifest/application/@android:label", buildInfo.AppName, xmlPokeSettings);
    XmlPoke(androidManifest, "/manifest/@package", buildInfo.PackageName, xmlPokeSettings);

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