using System.Collections.ObjectModel;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Text;

namespace GPSaveConverter
{
    /// <summary>
    /// Functions related to Windows powershell
    /// </summary>
    static class ScriptManager
    {
        public static string RunScript(string scriptText)
        {
            // Create a runspace and open it.
            using Runspace runspace = RunspaceFactory.CreateRunspace();
            runspace.Open();

            // Use the PowerShell class to execute commands in the runspace.
            using PowerShell ps = PowerShell.Create(runspace);

            // Set the execution policy to bypass for the current process.
            ps.AddScript("Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass");

            // Add the script to the pipeline
            ps.AddScript(scriptText);

            // Add an extra command to transform the script output objects into
            // nicely formatted strings.
            ps.AddCommand("Out-String");

            // Execute the script
            Collection<PSObject> results = ps.Invoke();

            // Check for errors in the error stream
            if (ps.Streams.Error.Count > 0)
            {
                StringBuilder errorBuilder = new StringBuilder();
                errorBuilder.AppendLine("PowerShell script execution failed with the following errors:");
                foreach (ErrorRecord error in ps.Streams.Error)
                {
                    errorBuilder.AppendLine(error.ToString());
                }
                throw new System.InvalidOperationException(errorBuilder.ToString());
            }

            // Convert the script result into a single string
            StringBuilder stringBuilder = new StringBuilder();
            foreach (PSObject obj in results)
            {
                stringBuilder.AppendLine(obj.ToString());
            }

            return stringBuilder.ToString();
        }
    }
}
