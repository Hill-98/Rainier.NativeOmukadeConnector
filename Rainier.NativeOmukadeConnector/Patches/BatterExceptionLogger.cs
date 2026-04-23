/*************************************************************************
* Rainier Native Omukade Connector
* (c) 2022 Hastwell/Electrosheep Networks 
* 
* This program is free software: you can redistribute it and/or modify
* it under the terms of the GNU Affero General Public License as published
* by the Free Software Foundation, either version 3 of the License, or
* (at your option) any later version.
* 
* This program is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
* GNU Affero General Public License for more details.
* 
* You should have received a copy of the GNU Affero General Public License
* along with this program.  If not, see <http://www.gnu.org/licenses/>.
**************************************************************************/

using HarmonyLib;
using System;
using System.Diagnostics;
using System.Reflection;
using System.Text;

namespace Rainier.NativeOmukadeConnector.Patches
{
    [HarmonyPatch(typeof(RainierLogHandler))]
    internal class BetterExceptionLogger
    {
        [HarmonyPatch(nameof(RainierLogHandler.LogException))]
        [HarmonyPrefix]
        internal static bool LogException(Exception exception)
        {
            Plugin.SharedLogger.LogError(PrepareExceptionString(exception));

            if(exception is AggregateException ae)
            {
                foreach(Exception ie in ae.InnerExceptions)
                {
                    Plugin.SharedLogger.LogError(PrepareExceptionString(ie));
                }
            }
            else if(exception.InnerException != null)
            {
                Plugin.SharedLogger.LogError(PrepareExceptionString(exception.InnerException));
            }

            return false;
        }

        static internal string PrepareExceptionString(Exception exception)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(exception.GetType().FullName);
            if (!string.IsNullOrWhiteSpace(exception.Message))
            {
                sb.Append(" - ");
                sb.Append(exception.Message);
            }
            sb.AppendLine();
            sb.AppendLine("Stack Trace:");

            StackTrace st = new StackTrace(exception);
            PrepareStacktraceString(sb, st);

            return sb.ToString();
        }

        static internal void PrepareStacktraceString(StringBuilder sb, StackTrace st)
        {
            foreach (System.Diagnostics.StackFrame frame in st.GetFrames())
            {
                try
                {
                    MethodBase frameMethod = frame.GetMethod();
                    if (frameMethod == null)
                    {
                        sb.Append("[null method] - at IL_");
                    }
                    else
                    {
                        string frameClass = frameMethod.DeclaringType.FullName;
                        string frameMethodDisplayName = frameMethod.Name;
                        int frameMetadataToken = frameMethod.MetadataToken;
                        sb.Append($"{frameClass}::{frameMethodDisplayName} @{frameMetadataToken:X8} - at IL_");
                    }
                    sb.Append(frame.GetILOffset().ToString("X4"));
                    sb.AppendLine();
                }
                catch (Exception ex)
                {
                    sb.AppendLine($"[error on this frame - {ex.GetType().FullName} - {ex.Message}]");
                }
            }
        }
    }
}
