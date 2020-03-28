#addin "Cake.Xamarin"

var target = Argument("target", (string)null);

//====================================================================
// Consts

// General
const string PATH_TO_SOLUTION = "TastyFormsApp.sln";

// Android
const string PATH_TO_ANDROID_PROJECT = "TastyFormsApp.Android/TastyFormsApp.Android.csproj";

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
RunTarget(target);