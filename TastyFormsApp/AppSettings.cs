namespace TastyFormsApp
{
    public class AppSettings
    {
        #if DEBUG
        
        public const string ApiUrl = "https://dev.tastyformsapp.com";
        
        #else

        public const string ApiUrl = "<%API_URL%>";
        
        #endif
    }
}