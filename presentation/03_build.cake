#addin "Cake.Xamarin"

var target = Argument("target", (string)null);

//====================================================================
// Consts

// General
const string PATH_TO_SOLUTION = "TastyFormsApp.sln";
const string PATH_TO_UNIT_TESTS_PROJECT = "TastyFormsApp.Tests/TastyFormsApp.Tests.csproj";

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
  .Does(()) => 
{
    var apkFilePath = BuildAndroidApk(PATH_TO_ANDROID_PROJECT, sign: true);
    MoveAppPackageToPackagesFolder(apkFilePath);
});

//====================================================================
// Publish iOS IPA

Task("PublishIPA")
  .IsDependentOn("RunUnitTests")
  .IsDependentOn("UpdateConfigFiles")
  .Does(()) =>
  {
        var ipaFilePath = BuildiOSIpa(PATH_TO_IOS_PROJECT, "Release");
        MoveAppPackageToPackagesFolder(ipaFilePath);
  });

RunTarget(target);