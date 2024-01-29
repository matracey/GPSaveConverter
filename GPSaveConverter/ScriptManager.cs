using System.Collections.ObjectModel;
using System.Management.Automation;
using System.Text;

namespace GPSaveConverter
{
    /// <summary>
    /// Functions related to Windows powershell
    /// </summary>
    static class ScriptManager
    {
        /// <summary>
        /// Executes a PowerShell script and returns the result as a string.
        /// </summary>
        /// <param name="scriptText">The PowerShell script to be executed.</param>
        /// <returns>A string representation of the script result.</returns>
        public static string RunScript(string scriptText)
        {
            // create Powershell runspace
            using var powershell = PowerShell.Create();
            // feed it the script text
            powershell.AddScript(scriptText);

            // add an extra command to transform the script
            // output objects into nicely formatted strings

            // remove this line to get the actual objects
            // that the script returns. For example, the script

            // "Get-Process" returns a collection
            // of System.Diagnostics.Process instances.
            // powershell.Commands.AddCommand("Out-String");

            // execute the script
            Collection<PSObject> results = powershell.Invoke();

            // convert the script result into a single string
            var stringBuilder = new StringBuilder();
            foreach (var obj in results)
            {
                stringBuilder.AppendLine(obj.ToString());
            }

            return stringBuilder.ToString();
        }
    }
}
