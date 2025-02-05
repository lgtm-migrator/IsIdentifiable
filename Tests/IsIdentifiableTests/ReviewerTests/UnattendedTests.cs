﻿using System;
using System.IO;
using FAnsi.Implementation;
using FAnsi.Implementations.MicrosoftSQL;
using FAnsi.Implementations.MySql;
using FAnsi.Implementations.Oracle;
using FAnsi.Implementations.PostgreSql;
using IsIdentifiable;
using NUnit.Framework;
using IsIdentifiable.Options;
using IsIdentifiable.Redacting;

namespace IsIdentifiableTests.ReviewerTests;

public class UnattendedTests
{
    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        ImplementationManager.Load<MicrosoftSQLImplementation>();
        ImplementationManager.Load<MySqlImplementation>();
        ImplementationManager.Load<PostgreSqlImplementation>();
        ImplementationManager.Load<OracleImplementation>();
    }

    [Test]
    public void NoFileToProcess_Throws()
    {
        var ex = Assert.Throws<Exception>(() => new UnattendedReviewer(new IsIdentifiableReviewerOptions(),null, new IgnoreRuleGenerator(),new RowUpdater() ));
        Assert.AreEqual("Unattended requires a file of errors to process",ex.Message);
    }

    [Test]
    public void NonExistantFileToProcess_Throws()
    {
        var ex = Assert.Throws<FileNotFoundException>(() => new UnattendedReviewer(new IsIdentifiableReviewerOptions()
        {
            FailuresCsv = "troll.csv"
        },null, new IgnoreRuleGenerator(),new RowUpdater()));
        StringAssert.Contains("Could not find Failures file",ex.Message);
    }
        
    [Test]
    public void NoTarget_Throws()
    {
        var fi = Path.Combine(TestContext.CurrentContext.WorkDirectory, "myfile.txt");
        File.WriteAllText(fi,"fff");
            
        var ex = Assert.Throws<Exception>(() => new UnattendedReviewer(new IsIdentifiableReviewerOptions()
        {
            FailuresCsv = fi
        },null, new IgnoreRuleGenerator(),new RowUpdater()));
        StringAssert.Contains("A single Target must be supplied for database updates",ex.Message);
    }

    [Test]
    public void NoOutputPath_Throws()
    {
        var fi = Path.Combine(TestContext.CurrentContext.WorkDirectory, "myfile.txt");
        File.WriteAllText(fi,"fff");
            
        var ex = Assert.Throws<Exception>(() => new UnattendedReviewer(new IsIdentifiableReviewerOptions()
        {
            FailuresCsv = fi
        },new Target(), new IgnoreRuleGenerator(),new RowUpdater()));
        StringAssert.Contains("An output path must be specified ",ex.Message);
    }

        
    [Test]
    public void Passes_NoFailures()
    {
        var fi = Path.Combine(TestContext.CurrentContext.WorkDirectory, "myfile.csv");
        File.WriteAllText(fi,"fff");

        var fiOut = Path.Combine(TestContext.CurrentContext.WorkDirectory, "out.csv");

        var reviewer = new UnattendedReviewer(new IsIdentifiableReviewerOptions()
        {
            FailuresCsv = fi,
            UnattendedOutputPath = fiOut
        }, new Target(), new IgnoreRuleGenerator(),new RowUpdater());

        Assert.AreEqual(0,reviewer.Run());
            
        //just the headers
        StringAssert.AreEqualIgnoringCase("Resource,ResourcePrimaryKey,ProblemField,ProblemValue,PartWords,PartClassifications,PartOffsets",File.ReadAllText(fiOut).TrimEnd());
    }

    [Test]
    public void Passes_FailuresAllUnprocessed()
    {
        var inputFile = @"Resource,ResourcePrimaryKey,ProblemField,ProblemValue,PartWords,PartClassifications,PartOffsets
FunBooks.HappyOzz,1.2.3,Narrative,We aren't in Kansas anymore Toto,Kansas###Toto,Location###Location,13###28";

        var fi = Path.Combine(TestContext.CurrentContext.WorkDirectory, "myfile.csv");
        File.WriteAllText(fi,inputFile);

        var fiOut = Path.Combine(TestContext.CurrentContext.WorkDirectory, "out.csv");
            
        //cleanup any remnant Allowlist or Reportlists
        var fiAllowlist = Path.Combine(TestContext.CurrentContext.WorkDirectory,IgnoreRuleGenerator.DefaultFileName);
        var fiReportlist = Path.Combine(TestContext.CurrentContext.WorkDirectory, RowUpdater.DefaultFileName);

        if(File.Exists(fiAllowlist))
            File.Delete(fiAllowlist);
            
        if(File.Exists(fiReportlist))
            File.Delete(fiReportlist);
            
        var reviewer = new UnattendedReviewer(new IsIdentifiableReviewerOptions()
        {
            FailuresCsv = fi,
            UnattendedOutputPath = fiOut
        }, new Target(), new IgnoreRuleGenerator(),new RowUpdater());

        Assert.AreEqual(0,reviewer.Run());
            
        //all that we put in is unprocessed so should come out the same
        TestHelpers.AreEqualIgnoringCaseAndLineEndings(inputFile,File.ReadAllText(fiOut).TrimEnd());

        Assert.AreEqual(1,reviewer.Total);
        Assert.AreEqual(0,reviewer.Ignores);
        Assert.AreEqual(1,reviewer.Unresolved);
        Assert.AreEqual(0,reviewer.Updates);
    }
        
    [TestCase(true)]
    [TestCase(false)]
    public void Passes_FailuresAllIgnored(bool rulesOnly)
    {
        var inputFile = @"Resource,ResourcePrimaryKey,ProblemField,ProblemValue,PartWords,PartClassifications,PartOffsets
FunBooks.HappyOzz,1.2.3,Narrative,We aren't in Kansas anymore Toto,Kansas###Toto,Location###Location,13###28";

        var fi = Path.Combine(TestContext.CurrentContext.WorkDirectory, "myfile.csv");
        File.WriteAllText(fi,inputFile);

        var fiOut = Path.Combine(TestContext.CurrentContext.WorkDirectory, "out.csv");
            
        //cleanup any remnant Allowlist or Reportlists
        var fiAllowlist = Path.Combine(TestContext.CurrentContext.WorkDirectory,IgnoreRuleGenerator.DefaultFileName);
        var fiReportlist = Path.Combine(TestContext.CurrentContext.WorkDirectory, RowUpdater.DefaultFileName);

        if(File.Exists(fiAllowlist))
            File.Delete(fiAllowlist);
            
        if(File.Exists(fiReportlist))
            File.Delete(fiReportlist);

        //add a Allowlist to ignore these
        File.WriteAllText(fiAllowlist,
            @"
- Action: Ignore
  IfColumn: Narrative
  IfPattern: ^We\ aren't\ in\ Kansas\ anymore\ Toto$");
            
        var reviewer = new UnattendedReviewer(new IsIdentifiableReviewerOptions()
        {
            FailuresCsv = fi,
            UnattendedOutputPath = fiOut,
            OnlyRules = rulesOnly
        }, new Target(), new IgnoreRuleGenerator(),new RowUpdater());

        Assert.AreEqual(0,reviewer.Run());
            
        //headers only since Allowlist eats the rest
        StringAssert.AreEqualIgnoringCase(@"Resource,ResourcePrimaryKey,ProblemField,ProblemValue,PartWords,PartClassifications,PartOffsets",File.ReadAllText(fiOut).TrimEnd());

        Assert.AreEqual(1,reviewer.Total);
        Assert.AreEqual(1,reviewer.Ignores);
        Assert.AreEqual(0,reviewer.Unresolved);
        Assert.AreEqual(0,reviewer.Updates);
    }

    [TestCase(true)]
    [TestCase(false)]
    public void Passes_FailuresAllUpdated(bool ruleCoversThis)
    {
        var inputFile = @"Resource,ResourcePrimaryKey,ProblemField,ProblemValue,PartWords,PartClassifications,PartOffsets
FunBooks.HappyOzz,1.2.3,Narrative,We aren't in Kansas anymore Toto,Kansas###Toto,Location###Location,13###28";

        var fi = Path.Combine(TestContext.CurrentContext.WorkDirectory, "myfile.csv");
        File.WriteAllText(fi,inputFile);

        var fiOut = Path.Combine(TestContext.CurrentContext.WorkDirectory, "out.csv");
            
        //cleanup any remnant Allowlist or Reportlists
        var fiAllowlist = Path.Combine(TestContext.CurrentContext.WorkDirectory,IgnoreRuleGenerator.DefaultFileName);
        var fiReportlist = Path.Combine(TestContext.CurrentContext.WorkDirectory, RowUpdater.DefaultFileName);

        if(File.Exists(fiAllowlist))
            File.Delete(fiAllowlist);
            
        if(File.Exists(fiReportlist))
            File.Delete(fiReportlist);

        //add a Reportlist to UPDATE these
        if(ruleCoversThis)
        {
            File.WriteAllText(fiReportlist,
                @"
- Action: Ignore
  IfColumn: Narrative
  IfPattern: ^We\ aren't\ in\ Kansas\ anymore\ Toto$");
        }           
            
        var reviewer = new UnattendedReviewer(new IsIdentifiableReviewerOptions()
        {
            FailuresCsv = fi,
            UnattendedOutputPath = fiOut,
            OnlyRules = true //prevents it going to the database
        }, new Target(), new IgnoreRuleGenerator(),new RowUpdater());

        Assert.AreEqual(0,reviewer.Run());
            
        //it matches the UPDATE rule but since OnlyRules is true it didn't actually update the database! so the record should definitely be in the output
            
        if(!ruleCoversThis)
        {
            // no rule covers this so the miss should appear in the output file

            TestHelpers.AreEqualIgnoringCaseAndLineEndings(@"Resource,ResourcePrimaryKey,ProblemField,ProblemValue,PartWords,PartClassifications,PartOffsets
FunBooks.HappyOzz,1.2.3,Narrative,We aren't in Kansas anymore Toto,Kansas###Toto,Location###Location,13###28", File.ReadAllText(fiOut).TrimEnd());
        }
        else
        {

            // a rule covers this so even though we do not update the database there shouldn't be a 'miss' in the output file

            TestHelpers.AreEqualIgnoringCaseAndLineEndings(@"Resource,ResourcePrimaryKey,ProblemField,ProblemValue,PartWords,PartClassifications,PartOffsets",
                File.ReadAllText(fiOut).TrimEnd());

        }
            

        Assert.AreEqual(1,reviewer.Total);
        Assert.AreEqual(0,reviewer.Ignores);
        Assert.AreEqual(ruleCoversThis ? 0 : 1,reviewer.Unresolved);
        Assert.AreEqual(ruleCoversThis ? 1 : 0, reviewer.Updates);
    }
}