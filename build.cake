#addin "Cake.Xamarin"
#addin "Cake.AppCenter"
#addin "Cake.Plist"
#addin "Cake.AndroidAppManifest"
#addin "Cake.Fastlane"
#tool "nuget:?package=GitVersion.CommandLine&version=5.0.1" // Reference older version because newest doesn't work on macOS.

var target = Argument("target", (string)null);
var environmentArg = Argument("env", (string)null);

//====================================================================
// Consts

// Environment
const string DEV_ENV = "dev";
const string STAGING_ENV = "staging";
const string PROD_ENV = "prod";

// General
const string APP_NAME="TastyFormsApp";
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
readonly string GOOGLE_PLAY_CONSOLE_JSON_KEY_FILE_PATH = EnvironmentVariable("GOOGLE_PLAY_CONSOLE_JSON_KEY_FILE_PATH");

// iOS
const string PATH_TO_IOS_PROJECT = "TastyFormsApp.iOS/TastyFormsApp.iOS.csproj";
const string PATH_TO_INFO_PLIST_FILE = "TastyFormsApp.iOS/Info.plist";
readonly string APP_STORE_CONNECT_API_JSON_KEY_FILE_PATH = EnvironmentVariable("APP_STORE_CONNECT_API_JSON_KEY_FILE_PATH");

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
    public int BuildNumber { get; }
    public string AppVersion { get; }
    public string AppName { get; }
    public string PackageName { get; }
    public string AndroidAppCenterAppName { get; }
    public string ApkFileName { get; }
    public string IosAppCenterAppName { get; }
    public string IpaFileName { get; }
    public string Environment { get; }

    public BuildInfo(
      string apiUrl, 
      int buildNumber, 
      string appVersion,
      string appName,
      string packageName,
      string androidAppCenterAppName,
      string apkFileName,
      string iosAppCenterAppName,
      string ipaFileName,
      string environment)
    {
        ApiUrl = apiUrl;
        BuildNumber = buildNumber;
        AppVersion = appVersion;
        AppName = appName;
        PackageName = packageName;
        AndroidAppCenterAppName = androidAppCenterAppName;
        ApkFileName = apkFileName;
        IosAppCenterAppName = iosAppCenterAppName;
        IpaFileName = ipaFileName;
        Environment = environment;
    }
}

//====================================================================
// Setups script.

Setup<BuildInfo>(setupContext => 
{
    var gitVersion = GitVersion();
    var apiUrl = "https://dev.tastyformsapp.com";
    var appName = $"{APP_NAME}.dev";
    var packageName = "com.tastyformsapp.dev";
    var androidAppCenterAppName = "TastyFormsApp/TastyFormsApp-Android-DEV";
    var iosAppCenterAppName = "TastyFormsApp/TastyFormsApp-iOS-DEV";
    var ipaFileName = $"{APP_NAME}.iOS.ipa";
    var currentEnvironment = GetEnvironment();

    if (currentEnvironment == STAGING_ENV)
    {
        apiUrl = "https://staging.tastyformsapp.com";
        appName = $"{APP_NAME}.staging";
        packageName = "com.tastyformsapp.staging";
        androidAppCenterAppName = "TastyFormsApp/TastyFormsApp-Android-staging";
        iosAppCenterAppName = "TastyFormsApp/TastyFormsApp-iOS-staging";
    }
    else if (currentEnvironment == PROD_ENV)
    {
        apiUrl = "https://prod.tastyformsapp.com";
        appName = APP_NAME;
        packageName = "com.tastyformsapp";
    }

    var apkFileName = $"{packageName}-Signed.apk";

    return new BuildInfo(
      apiUrl,
      buildNumber: (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
      appVersion: gitVersion.MajorMinorPatch,
      appName,
      packageName,
      androidAppCenterAppName,
      apkFileName,
      iosAppCenterAppName,
      ipaFileName,
      currentEnvironment);
});

public string GetEnvironment()
{
    if (String.IsNullOrEmpty(environmentArg))
    {
        var gitVersion = GitVersion();
        var branchName = gitVersion.BranchName;

        if (branchName.StartsWith("release/"))
        {
            return STAGING_ENV;
        }
        else if (branchName.Equals("master"))
        {
            return PROD_ENV;
        }
        else
        {
            return DEV_ENV;
        }
    }
    else
    {
        return environmentArg;
    }
}

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
    DotNetCoreRestore(PATH_TO_SOLUTION);
    
    NuGetRestore(PATH_TO_IOS_PROJECT);
    NuGetRestore(PATH_TO_ANDROID_PROJECT);
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
         Configuration = "Release",
         ArgumentCustomization = args=>args.Append("--logger trx")
     };

      DotNetCoreTest(PATH_TO_UNIT_TESTS_PROJECT, settings);
  });

//==================================================================== Android ====================================================================

//====================================================================
// Update Android Manifest

Task("UpdateAndroidManifest")
  .Does<BuildInfo>(buildInfo => 
{
    var androidManifestFilePath = new FilePath(PATH_TO_ANDROID_MANIFEST_FILE);
    var manifest = DeserializeAppManifest(androidManifestFilePath);

    manifest.VersionName = buildInfo.AppVersion;
    manifest.VersionCode = buildInfo.BuildNumber;
    manifest.ApplicationLabel = buildInfo.AppName;
    manifest.PackageName = buildInfo.PackageName;
    manifest.Debuggable = false;

    SerializeAppManifest(androidManifestFilePath, manifest);
});

//====================================================================
// Publish Android APK

Task("PublishAPK")
  .IsDependentOn("RunUnitTests")
  .IsDependentOn("UpdateConfigFiles")
  .IsDependentOn("UpdateAndroidManifest")
  .Does<BuildInfo>(buildInfo => 
{
    FilePath apkFilePath = null;

    if (buildInfo.Environment == PROD_ENV)
    {
        apkFilePath = BuildAndroidApk(PATH_TO_ANDROID_PROJECT, sign: true, configurator: msBuildSettings => 
        {
            msBuildSettings.WithProperty("AndroidKeyStore", "True")
                           .WithProperty("AndroidSigningKeyAlias", ANDROID_KEYSTORE_ALIAS)
                           .WithProperty("AndroidSigningKeyPass", ANDROID_KEYSTORE_PASSWORD)
                           .WithProperty("AndroidSigningKeyStore", PATH_TO_ANDROID_KEYSTORE_FILE)
                           .WithProperty("AndroidSigningStorePass", ANDROID_KEYSTORE_PASSWORD);
        });
    }
    else
    {
        apkFilePath = BuildAndroidApk(PATH_TO_ANDROID_PROJECT, sign: true);
    }

    MoveAppPackageToPackagesFolder(apkFilePath);
});

//====================================================================
// Deploy APK to Google Play Internal track

Task("DeployAPKToGooglePlayInternalTrack")
  .IsDependentOn("PublishAPK")
  .Does<BuildInfo>(buildInfo => 
  {
           var configuration = new FastlaneSupplyConfiguration
           {
                 ApkFilePath = $"{APP_PACKAGE_FOLDER_NAME}/{buildInfo.ApkFileName}",
                 JsonKeyFilePath = GOOGLE_PLAY_CONSOLE_JSON_KEY_FILE_PATH,
                 MetadataPath = "AndroidMetadata",
                 SkipUploadMetadata = true,
                 SkipUploadImages = true,
                 SkipUploadScreenShots = true,
                 Track = "internal",
                 PackageName = buildInfo.PackageName
           };

           Fastlane.Supply(configuration);
  });

//====================================================================
// Deploys APK to App Center

Task("DeployAPKToAppCenter")
  .IsDependentOn("PublishAPK")
  .Does<BuildInfo>(buildInfo => 
{
    if (buildInfo.Environment == PROD_ENV)
    {
        throw new InvalidOperationException("Master branch being deployed to App Center.");
    }

    AppCenterDistributeRelease(new AppCenterDistributeReleaseSettings
    {
        File = $"{APP_PACKAGE_FOLDER_NAME}/{buildInfo.ApkFileName}",
        Group = "Collaborators",
        App = buildInfo.AndroidAppCenterAppName,
        Token = APP_CENTER_TOKEN
    });
});

//==================================================================== iOS ====================================================================

//====================================================================
// Update iOS Info.plist

Task("UpdateIosInfoPlist")
  .Does<BuildInfo>(buildInfo =>
  {
    var plist = File(PATH_TO_INFO_PLIST_FILE);
    dynamic data = DeserializePlist(plist);

    data["CFBundleShortVersionString"] = buildInfo.AppVersion;
    data["CFBundleVersion"] = buildInfo.BuildNumber.ToString();
    data["CFBundleName"] = buildInfo.AppName;
    data["CFBundleDisplayName"] = buildInfo.AppName;
    data["CFBundleIdentifier"] = buildInfo.PackageName;

    SerializePlist(plist, data);
  });

//====================================================================
// Publish iOS IPA

Task("PublishIPA")
  .IsDependentOn("RunUnitTests")
  .IsDependentOn("UpdateConfigFiles")
  .IsDependentOn("UpdateIosInfoPlist")
  .Does<BuildInfo>(buildInfo =>
  {
    var buildConfiguration = "Release";

    if (buildInfo.Environment == PROD_ENV)
    {
        buildConfiguration = "AppStore";
    }

    var ipaFilePath = BuildiOSIpa(PATH_TO_IOS_PROJECT, buildConfiguration);
    MoveAppPackageToPackagesFolder(ipaFilePath);
  });

//====================================================================
// Deploys IPA to App Center
Task("DeployIPAToAppCenter")
  .IsDependentOn("PublishIPA")
  .Does<BuildInfo>(buildInfo => 
{
    if (buildInfo.Environment == PROD_ENV)
    {
        throw new InvalidOperationException("Master branch being deployed to App Center.");
    }

    AppCenterDistributeRelease(new AppCenterDistributeReleaseSettings
    {
        File = $"{APP_PACKAGE_FOLDER_NAME}/{buildInfo.IpaFileName}",
        Group = "Collaborators",
        App = buildInfo.IosAppCenterAppName,
        Token = APP_CENTER_TOKEN
    });
});

//====================================================================
// Deploys IPA to Test Flight
Task("DeployIPAToTestFlight")
  .IsDependentOn("PublishIPA")
  .Does<BuildInfo>(buildInfo => 
{
  Information("Preparing to upload new build number: " + buildInfo.BuildNumber);

  var ipaPath = MakeAbsolute(Directory($"{APP_PACKAGE_FOLDER_NAME}/{buildInfo.IpaFileName}"));
  StartProcess("fastlane", new ProcessSettings { Arguments = "pilot upload --ipa " + ipaPath + " --skip_waiting_for_build_processing true --api_key_path " + APP_STORE_CONNECT_API_JSON_KEY_FILE_PATH});
});

//==================================================================== App Center ====================================================================

//====================================================================
// Deploys APK and IPA to App Center

Task("DeployAPKAndIPAToAppCenter")
  .IsDependentOn("DeployAPKToAppCenter")
  .IsDependentOn("DeployIPAToAppCenter")
  .Does<BuildInfo>(buildInfo => 
{
});

//====================================================================

RunTarget(target);