using System;
using System.IO;
using System.Linq;
using System.Reflection;

namespace CatLib.Config;

public static class AuthorLocator
{
    public static string Find(string pluginDirectory, string company, string assemblyName)
    {
        var fromPackage = FromPackageFolder(pluginDirectory);
        if (fromPackage != null)
        {
            return fromPackage;
        }

        if (string.IsNullOrWhiteSpace(company) || string.Equals(company.Trim(), assemblyName, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return company.Trim();
    }

    public static string FromPackageFolder(string pluginDirectory)
    {
        if (string.IsNullOrWhiteSpace(pluginDirectory))
        {
            return null;
        }

        try
        {
            var directory = new DirectoryInfo(pluginDirectory);
            for (var level = 0; directory != null && level <= IconLocator.MaxLevelsUp; level++)
            {
                var parent = directory.Parent;
                if (parent != null && string.Equals(parent.Name, "plugins", StringComparison.OrdinalIgnoreCase))
                {
                    var dash = directory.Name.IndexOf('-');
                    return dash > 0 && dash < directory.Name.Length - 1 ? directory.Name.Substring(0, dash) : null;
                }

                directory = parent;
            }
        }
        catch (Exception)
        {
        }

        return null;
    }

    public static string Company(Assembly assembly)
    {
        try
        {
            return assembly.GetCustomAttributes(typeof(AssemblyCompanyAttribute), false).OfType<AssemblyCompanyAttribute>().FirstOrDefault()?.Company;
        }
        catch (Exception)
        {
            return null;
        }
    }
}
