#addin "Cake.Xamarin"
#addin "Cake.AppCenter"
#tool "nuget:?package=GitVersion.CommandLine&version=5.0.1" // Reference older version because newest doesn't work on macOS.

var target = Argument("target", (string)null);

//====================================================================
// Consts

// General
const string PATH_TO_SOLUTION = "TastyFormsApp.sln";
const string PATH_TO_UNIT_TESTS_PROJECT = "TastyFormsApp.Tests/TastyFormsApp.Tests.csproj";
const string APP_PACKAGE_FOLDER_NAME = "AppPackages";
readonly string APP_CENTER_TOKEN = EnvironmentVariable("TASTYFORMSAPP_APP_CENTER_TOKEN");

// Android
const string PATH_TO_ANDROID_PROJECT = "TastyFormsApp.Android/TastyFormsApp.Android.csproj";
const string PATH_TO_ANDROID_MANIFEST_FILE = "TastyFormsApp.Android/Properties/AndroidManifest.xml";
readonly string PATH_TO_ANDROID_KEYSTORE_FILE = EnvironmentVariable("TASTYFORMSAPP_KEYSTORE_PATH");
readonly string ANDROID_KEYSTORE_ALIAS = EnvironmentVariable("TASTYFORMSAPP_KEYSTORE_ALIAS");
readonly string ANDROID_KEYSTORE_PASSWORD = EnvironmentVariable("TASTYFORMSAPP_KEYSTORE_PASSWORD");

// iOS
const string PATH_TO_IOS_PROJECT = "TastyFormsApp.iOS/TastyFormsApp.iOS.csproj";
const string PATH_TO_INFO_PLIST_FILE = "TastyFormsApp.iOS/Info.plist";

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
    public string AndroidAppCenterAppName { get; }

    public BuildInfo(
      string apiUrl, 
      string buildNumber, 
      string appVersion,
      string appName,
      string packageName,
      string androidAppCenterAppName)
    {
        ApiUrl = apiUrl;
        BuildNumber = buildNumber;
        AppVersion = appVersion;
        AppName = appName;
        PackageName = packageName;
        AndroidAppCenterAppName = androidAppCenterAppName;
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
    var androidAppCenterAppName = "TastyFormsApp/TastyFormsApp-Android-DEV";

    if (branchName.StartsWith("release/"))
    {
        apiUrl = "https://staging.tastyformsapp.com";
        appName = "TastyFormsApp.staging";
        packageName = "com.tastyformsapp.staging";
        androidAppCenterAppName = "TastyFormsApp/TastyFormsApp-Android-staging";
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
      packageName,
      androidAppCenterAppName);
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

    var androidManifestFilePath = PATH_TO_ANDROID_MANIFEST_FILE;
    XmlPoke(androidManifestFilePath, "/manifest/@android:versionName", buildInfo.AppVersion, xmlPokeSettings);
    XmlPoke(androidManifestFilePath, "/manifest/@android:versionCode", buildInfo.BuildNumber, xmlPokeSettings);
    XmlPoke(androidManifestFilePath, "/manifest/application/@android:label", buildInfo.AppName, xmlPokeSettings);
    XmlPoke(androidManifestFilePath, "/manifest/@package", buildInfo.PackageName, xmlPokeSettings);

    var gitVersion = GitVersion();

    if (gitVersion.BranchName == "master")
    {
        var apkFilePath = BuildAndroidApk(PATH_TO_ANDROID_PROJECT, sign: true, configurator: msBuildSettings => 
        {
            msBuildSettings.WithProperty("AndroidKeyStore", "True")
                           .WithProperty("AndroidSigningKeyAlias", ANDROID_KEYSTORE_ALIAS)
                           .WithProperty("AndroidSigningKeyPass", ANDROID_KEYSTORE_PASSWORD)
                           .WithProperty("AndroidSigningKeyStore", PATH_TO_ANDROID_KEYSTORE_FILE)
                           .WithProperty("AndroidSigningStorePass", ANDROID_KEYSTORE_PASSWORD);
        });

        MoveAppPackageToPackagesFolder(apkFilePath);
    }
    else
    {
        var apkFilePath = BuildAndroidApk(PATH_TO_ANDROID_PROJECT, sign: true);
        MoveAppPackageToPackagesFolder(apkFilePath);
    }
});

//====================================================================
// Deploys APK to App Center

Task("DeployAPKToAppCenter")
  .IsDependentOn("PublishAPK")
  .Does<BuildInfo>(buildInfo => 
{
    var gitVersion = GitVersion();

    if (gitVersion.BranchName.Equals("master"))
    {
        throw new InvalidOperationException("Master branch being deployed to App Center.");
    }

    AppCenterDistributeRelease(new AppCenterDistributeReleaseSettings
    {
        File = $"{APP_PACKAGE_FOLDER_NAME}/{buildInfo.PackageName}-Signed.apk",
        Group = "Collaborators",
        App = buildInfo.AndroidAppCenterAppName,
        Token = APP_CENTER_TOKEN
    });
});

//====================================================================

Task("PublishIPA")
  .IsDependentOn("RunUnitTests")
  .IsDependentOn("UpdateConfigFiles")
  .Does<BuildInfo>(buildInfo =>
  {
    var iOSplist = PATH_TO_INFO_PLIST_FILE;

    var xmlPokeSettings = new XmlPokeSettings
    {
        DtdProcessing = XmlDtdProcessing.Parse
    };

    XmlPoke(iOSplist, "/plist/dict/key[text()='CFBundleShortVersionString']/following-sibling::string[1]", buildInfo.AppVersion, xmlPokeSettings);
    XmlPoke(iOSplist, "/plist/dict/key[text()='CFBundleVersion']/following-sibling::string[1]", buildInfo.BuildNumber, xmlPokeSettings);
    XmlPoke(iOSplist, "/plist/dict/key[text()='CFBundleName']/following-sibling::string[1]", buildInfo.AppName, xmlPokeSettings);
    XmlPoke(iOSplist, "/plist/dict/key[text()='CFBundleDisplayName']/following-sibling::string[1]", buildInfo.AppName, xmlPokeSettings);
    XmlPoke(iOSplist, "/plist/dict/key[text()='CFBundleIdentifier']/following-sibling::string[1]", buildInfo.PackageName, xmlPokeSettings);

    var buildConfiguration = "Release";
    var gitVersion = GitVersion();

    if (gitVersion.BranchName.Equals("master"))
    {
        buildConfiguration = "AppStore";
    }

    var ipaFilePath = BuildiOSIpa(PATH_TO_IOS_PROJECT, buildConfiguration);
    MoveAppPackageToPackagesFolder(ipaFilePath);
  });

RunTarget(target);