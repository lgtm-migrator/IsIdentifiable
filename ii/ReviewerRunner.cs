﻿using FAnsi.Implementation;
using FAnsi.Implementations.MicrosoftSQL;
using FAnsi.Implementations.MySql;
using FAnsi.Implementations.Oracle;
using FAnsi.Implementations.PostgreSql;
using IsIdentifiable.Options;
using Terminal.Gui;
using YamlDotNet.Serialization;

namespace IsIdentifiable.Redacting;

public class ReviewerRunner
{
    private readonly IsIdentifiableBaseOptions? _analyserOpts;
    private readonly IsIdentifiableReviewerOptions _reviewerOptions;

    public ReviewerRunner(IsIdentifiableBaseOptions? analyserOpts, IsIdentifiableReviewerOptions reviewerOptions)
    {
        _analyserOpts = analyserOpts;
        _reviewerOptions = reviewerOptions;
    }

    /// <summary>
    /// Runs the reviewer gui or redaction mode
    /// </summary>
    /// <returns></returns>
    public int Run()
    {
        Deserializer d = new Deserializer();
        List<Target> targets;
        var logger = NLog.LogManager.GetCurrentClassLogger();

        try
        {
            var file = new FileInfo(_reviewerOptions.TargetsFile);

            // file does not exist
            if (!file.Exists)
            {
                if(_reviewerOptions.TargetsFile != IsIdentifiableReviewerOptions.TargetsFileDefault)
                {
                    logger.Error($"Could not find '{file.FullName}'");
                    return 1;
                }
                else
                {
                    // theres no Targets.yaml file but since that's the default
                    // we should just disable database stuff
                    _reviewerOptions.OnlyRules = true;
                    targets = new List<Target>();
                }
            }
            else
            {
                // there is a Targets file, read it
                var contents = File.ReadAllText(file.FullName);

                if (string.IsNullOrWhiteSpace(contents))
                {
                    logger.Error($"Targets file is empty '{file.FullName}'");
                    return 2;
                }

                targets = d.Deserialize<List<Target>>(contents);

                if (!targets.Any())
                {
                    logger.Error($"Targets file did not contain any valid targets '{file.FullName}'");
                    return 3;
                }
            }
        }
        catch (Exception e)
        {
            logger.Error(e, $"Error deserializing '{_reviewerOptions.TargetsFile}'");
            return 4;
        }

        if (_reviewerOptions.OnlyRules)
            logger.Info("Skipping Connection Tests");
        else
        {
            logger.Info("Running Connection Tests");

            ImplementationManager.Load<MicrosoftSQLImplementation>();
            ImplementationManager.Load<MySqlImplementation>();
            ImplementationManager.Load<PostgreSqlImplementation>();
            ImplementationManager.Load<OracleImplementation>();

            try
            {
                foreach (Target t in targets)
                    Console.WriteLine(t.Discover().Exists()
                        ? $"Successfully connected to {t.Name}"
                        : $"Failed to connect to {t.Name}");
            }
            catch (Exception e)
            {
                logger.Error(e, "Error Validating Targets");
                return 10;
            }
        }

        //for updater try to match the ProblemValue words
        var updater = new RowUpdater(new FileInfo(_reviewerOptions.Reportlist))
        {
            RulesOnly = _reviewerOptions.OnlyRules,
            RulesFactory = new MatchProblemValuesPatternFactory()
        };

        //for Ignorer match the whole string
        var ignorer = new IgnoreRuleGenerator(new FileInfo(_reviewerOptions.IgnoreList));

        try
        {
            if (!string.IsNullOrWhiteSpace(_reviewerOptions.UnattendedOutputPath))
            {
                //run unattended
                if (targets.Count != 1)
                    throw new Exception("Unattended requires a single entry in Targets");

                var unattended = new UnattendedReviewer(_reviewerOptions, targets.Single(), ignorer, updater);
                return unattended.Run();
            }
            else
            {
                Console.WriteLine("Press any key to launch GUI");
                Console.ReadKey();


                if (_reviewerOptions.UseSystemConsole)
                {
                    Application.UseSystemConsole = true;
                }


                //run interactive
                Application.Init();

                if (_reviewerOptions.Theme != null && File.Exists(_reviewerOptions.Theme))
                {
                    try
                    {
                        var des = new Deserializer();
                        var theme = des.Deserialize<TerminalGuiTheme>(File.ReadAllText(_reviewerOptions.Theme));

                        Colors.Base = theme.Base.GetScheme();
                        Colors.Dialog = theme.Dialog.GetScheme();
                        Colors.Error = theme.Error.GetScheme();
                        Colors.Menu = theme.Menu.GetScheme();
                        Colors.TopLevel = theme.TopLevel.GetScheme();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.ErrorQuery("Could not deserialize theme", ex.Message);
                    }
                }

                var top = Application.Top;

                var mainWindow = new MainWindow(_analyserOpts ?? new IsIdentifiableBaseOptions(), _reviewerOptions, ignorer, updater);


                // Creates the top-level window to show
                var win = new Window("IsIdentifiable Reviewer")
                {
                    X = 0,
                    Y = 1, // Leave one row for the toplevel menu

                    // By using Dim.Fill(), it will automatically resize without manual intervention
                    Width = Dim.Fill(),
                    Height = Dim.Fill()
                };

                top.Add(win);

                top.Add(mainWindow.Menu);

                win.Add(mainWindow.Body);

                Application.Run();

                return 0;
            }
        }
        catch (Exception e)
        {
            logger.Error(e, $"Application crashed");

            int tries = 5;
            while (Application.Top != null && tries-- > 0)
                try
                {
                    Application.RequestStop();
                }
                catch (Exception)
                {
                    Console.WriteLine("Failed to terminate GUI on crash");
                }

            return 99;
        }
        finally
        {
            Application.Shutdown();
        }
    }
}