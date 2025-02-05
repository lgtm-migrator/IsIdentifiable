﻿using NUnit.Framework;
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace IsIdentifiableTests;

/// <summary>
/// Tests to confirm that the dependencies in csproj files (NuGet packages) match those in the .nuspec files and that packages.md 
/// lists the correct versions (in documentation)
/// </summary>
public class NuspecIsCorrectTests
{
    static string[] Analyzers = new string[] { "coverlet.collector", "SecurityCodeScan.VS2019" };

    //core dependencies should be in all nuspec files
    [TestCase("../../../../../IsIdentifiable/IsIdentifiable.csproj", null, "../../../../../Packages.md")]
    public void TestDependencyCorrect(string csproj, string nuspec, string packagesMarkdown)
    {
        if (csproj != null && !Path.IsPathRooted(csproj))
            csproj = Path.Combine(TestContext.CurrentContext.TestDirectory, csproj);
        if (nuspec != null && !Path.IsPathRooted(nuspec))
            nuspec = Path.Combine(TestContext.CurrentContext.TestDirectory, nuspec);
        if (packagesMarkdown != null && !Path.IsPathRooted(packagesMarkdown))
            packagesMarkdown = Path.Combine(TestContext.CurrentContext.TestDirectory, packagesMarkdown);

        if (!File.Exists(csproj))
            Assert.Fail("Could not find file {0}", csproj);
        if (nuspec != null && !File.Exists(nuspec))
            Assert.Fail("Could not find file {0}", nuspec);

        if (packagesMarkdown != null && !File.Exists(packagesMarkdown))
            Assert.Fail("Could not find file {0}", packagesMarkdown);

        StringBuilder unlistedDependencies = new StringBuilder();
        StringBuilder undocumented = new StringBuilder();

        //<PackageReference Include="NUnit3TestAdapter" Version="3.13.0" />
        Regex rPackageRef = new Regex(@"<PackageReference\s+Include=""(.*)""\s+Version=""([^""]*)""", RegexOptions.IgnoreCase);

        //<dependency id="CsvHelper" version="12.1.2" />
        Regex rDependencyRef = new Regex(@"<dependency\s+id=""(.*)""\s+version=""([^""]*)""", RegexOptions.IgnoreCase);

        //For each dependency listed in the csproj
        foreach (Match p in rPackageRef.Matches(File.ReadAllText(csproj)))
        {
            string package = p.Groups[1].Value;
            string version = p.Groups[2].Value.Trim('[', ']');

            bool found = false;

            // Not one we need to pass on to the package consumers
            if (package.Contains("Microsoft.NETFramework.ReferenceAssemblies.net461"))
                continue;

            //analyzers do not have to be listed as a dependency in nuspec (but we should document them in packages.md)
            if (!Analyzers.Contains(package) && nuspec != null)
            {
                //make sure it appears in the nuspec
                foreach (Match d in rDependencyRef.Matches(File.ReadAllText(nuspec)))
                {
                    string packageDependency = d.Groups[1].Value;
                    string versionDependency = d.Groups[2].Value.Trim('[',']');

                    if (packageDependency.Equals(package))
                    {
                        Assert.AreEqual(version, versionDependency, "Package {0} is version {1} in {2} but version {3} in {4}", package, version, csproj, versionDependency, nuspec);
                        found = true;
                    }
                }

                if (!found)
                    unlistedDependencies.AppendLine(String.Format("Package {0} in {1} is not listed as a dependency of {2}. Recommended line is:\r\n{3}", package, csproj, nuspec,
                        BuildRecommendedDependencyLine(package, version)));
            }


            //And make sure it appears in the packages.md file
            if (packagesMarkdown != null)
            {
                found = false;
                foreach (string line in File.ReadAllLines(packagesMarkdown))
                {
                    if (Regex.IsMatch(line, $@"\|\s*[\s[]{Regex.Escape(package)}[\s\]]", RegexOptions.IgnoreCase))
                    {
                        int count = new Regex(Regex.Escape(version)).Matches(line).Count;

                        Assert.AreEqual(2, count, "Markdown file {0} did not contain 2 instances of the version {1} for package {2} in {3}", packagesMarkdown, version, package, csproj);
                        found = true;
                    }
                }

                if (!found)
                    undocumented.AppendLine(String.Format("Package {0} in {1} is not documented in {2}. Recommended line is:\r\n{3}", package, csproj, packagesMarkdown,
                        BuildRecommendedMarkdownLine(package, version)));
            }
        }

        Assert.IsEmpty(unlistedDependencies.ToString());
        Assert.IsEmpty(undocumented.ToString());
    }

    private object BuildRecommendedDependencyLine(string package, string version)
    {
        return string.Format("<dependency id=\"{0}\" version=\"{1}\" />", package, version);
    }

    private object BuildRecommendedMarkdownLine(string package, string version)
    {
        return string.Format("| {0} | [GitHub]() | [{1}](https://www.nuget.org/packages/{0}/{1}) | | | |", package, version);
    }
}
