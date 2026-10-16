using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using System.Diagnostics;

namespace NetPerf.Core {

    /// <summary>
    /// Get the version number
    /// </summary>
    public static class VersionInformation {

        public static string GetFileVersion(this Assembly assembly) {
            
            var fvi = FileVersionInfo.GetVersionInfo(assembly.Location);
            return fvi.ProductVersion;
            //return String.Format("{0}.{1}.{2}.{3}", fvi.ProductMajorPart, fvi.ProductMinorPart, fvi.ProductVersion, fvi.ProductBuildPart);
            
            //var sb = new StringBuilder();
            //sb.Append(assembly.GetName().Version.ToString(4));
            //sb.Append(".");
            //sb.Append(assembly.GetName().Version.Minor);
            //sb.Append(".");
            //sb.Append(assembly.GetName().Version.Build);
            //sb.Append(".");
            //sb.Append(assembly.GetName().Version.Revision);
            //return sb.ToString();
        }

        public static string Copyright(this Assembly assembly) {

            AssemblyCopyrightAttribute attr = assembly.GetCustomAttributes(typeof(AssemblyCopyrightAttribute), false).FirstOrDefault() as AssemblyCopyrightAttribute;
            if (attr != null) {
                return attr.Copyright;
            }
            return string.Empty;
        }

    }
}
