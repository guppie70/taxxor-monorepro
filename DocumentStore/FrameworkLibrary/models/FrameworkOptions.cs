namespace FrameworkLibrary.models
{

    /// <summary>
    /// Generic options for the framework
    /// </summary>
    public class FrameworkOptions
    {
        public bool DebugMode { get; set; }
        public bool RequestVariablesDebugOutput { get; set; }
        public string ProjectType { get; set; }
        public FrameworkOptions()
        {
            DebugMode = false;
            RequestVariablesDebugOutput = true;
            ProjectType = "site";
        }
    }
}