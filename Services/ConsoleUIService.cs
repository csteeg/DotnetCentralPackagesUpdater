using Spectre.Console;
using CentralNuGetUpdater.Models;

namespace CentralNuGetUpdater.Services;

public class ConsoleUIService
{
    private bool? _supportsAnsi;

    private bool SupportsAnsi
    {
        get
        {
            if (_supportsAnsi.HasValue)
                return _supportsAnsi.Value;

            try
            {
                // Test if ANSI is supported by checking the console profile
                _supportsAnsi = AnsiConsole.Profile.Capabilities.Interactive &&
                               AnsiConsole.Profile.Capabilities.Ansi;
                return _supportsAnsi.Value;
            }
            catch
            {
                _supportsAnsi = false;
                return false;
            }
        }
    }

    public void DisplayWelcome()
    {
        var rule = new Rule("[bold blue]Central NuGet Package Updater[/]")
        {
            Style = Style.Parse("blue")
        };
        AnsiConsole.Write(rule);
        AnsiConsole.WriteLine();
    }

    public void DisplayPackages(List<PackageInfo> packages)
    {
        if (!packages.Any())
        {
            AnsiConsole.MarkupLine("[yellow]No packages found in Directory.Packages.props[/]");
            return;
        }

        var packagesWithUpdates = packages.Where(p => p.HasUpdate).ToList();
        var packagesUpToDate = packages.Where(p => !p.HasUpdate && !string.IsNullOrEmpty(p.LatestVersion)).ToList();
        var packagesWithErrors = packages.Where(p => string.IsNullOrEmpty(p.LatestVersion)).ToList();

        if (packagesWithUpdates.Any())
        {
            AnsiConsole.MarkupLine("[bold green]Packages with available updates:[/]");
            var updateTable = new Table();
            updateTable.AddColumn("Package");
            updateTable.AddColumn("Current Version");
            updateTable.AddColumn("Latest Version");
            updateTable.AddColumn("Published");

            foreach (var package in packagesWithUpdates)
            {
                updateTable.AddRow(
                    $"[bold]{package.Id}[/]",
                    $"[red]{package.CurrentVersion}[/]",
                    $"[green]{package.LatestVersion}[/]",
                    package.Published?.ToString("yyyy-MM-dd") ?? "N/A"
                );
            }

            AnsiConsole.Write(updateTable);
            AnsiConsole.WriteLine();
        }

        if (packagesUpToDate.Any())
        {
            AnsiConsole.MarkupLine("[bold blue]Packages up to date:[/]");
            var upToDateTable = new Table();
            upToDateTable.AddColumn("Package");
            upToDateTable.AddColumn("Version");

            foreach (var package in packagesUpToDate)
            {
                upToDateTable.AddRow(
                    package.Id,
                    $"[green]{package.CurrentVersion}[/]"
                );
            }

            AnsiConsole.Write(upToDateTable);
            AnsiConsole.WriteLine();
        }

        if (packagesWithErrors.Any())
        {
            AnsiConsole.MarkupLine("[bold red]Packages with errors (couldn't check for updates):[/]");
            foreach (var package in packagesWithErrors)
            {
                AnsiConsole.MarkupLine($"[red]• {package.Id} ({package.CurrentVersion})[/]");
            }
            AnsiConsole.WriteLine();
        }
    }

    public List<PackageInfo> SelectPackagesToUpdate(List<PackageInfo> packages)
    {
        var packagesWithUpdates = packages.Where(p => p.HasUpdate).ToList();

        if (!packagesWithUpdates.Any())
        {
            AnsiConsole.MarkupLine("[yellow]No packages available for update.[/]");
            return new List<PackageInfo>();
        }

        AnsiConsole.MarkupLine("[bold]Select packages to update:[/]");

        var choices = packagesWithUpdates.Select(p =>
            $"{p.Id} ({p.CurrentVersion} → {p.LatestVersion})").ToList();

        var prompt = new MultiSelectionPrompt<string>()
            .Title("Which packages would you like to update?")
            .NotRequired()
            .PageSize(10)
            .MoreChoicesText("[grey](Move up and down to reveal more packages)[/]")
            .InstructionsText("[grey](Press [blue]<space>[/] to toggle a package, [green]<enter>[/] to accept)[/]")
            .AddChoices(choices);

        // Select all choices by default
        foreach (var choice in choices)
        {
            prompt.Select(choice);
        }

        if (SupportsAnsi)
        {
            try
            {
                var selectedPackages = AnsiConsole.Prompt(prompt);

                // Mark selected packages
                foreach (var package in packagesWithUpdates)
                {
                    var packageChoice = $"{package.Id} ({package.CurrentVersion} → {package.LatestVersion})";
                    package.IsSelected = selectedPackages.Contains(packageChoice);
                }

                return packagesWithUpdates.Where(p => p.IsSelected).ToList();
            }
            catch
            {
                // Fall through to simple console method
            }
        }

        // Fallback for terminals that don't support ANSI - select all packages by default
        Console.WriteLine("All packages will be updated (ANSI not supported for interactive selection):");
        for (int i = 0; i < packagesWithUpdates.Count; i++)
        {
            var package = packagesWithUpdates[i];
            Console.WriteLine($"{i + 1}. {package.Id} ({package.CurrentVersion} → {package.LatestVersion})");
            package.IsSelected = true; // Select all by default
        }

        Console.WriteLine("Press Enter to continue with all packages, or Ctrl+C to cancel...");
        Console.ReadLine();

        return packagesWithUpdates;
    }

    public bool ConfirmUpdate(List<PackageInfo> selectedPackages)
    {
        if (!selectedPackages.Any())
        {
            return false;
        }

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[bold]Summary of updates to be applied:[/]");

        var summaryTable = new Table();
        summaryTable.AddColumn("Package ID");
        summaryTable.AddColumn("Current Version");
        summaryTable.AddColumn("New Version");

        foreach (var package in selectedPackages)
        {
            summaryTable.AddRow(
                package.Id,
                $"[red]{package.CurrentVersion}[/]",
                $"[green]{package.LatestVersion}[/]"
            );
        }

        AnsiConsole.Write(summaryTable);
        AnsiConsole.WriteLine();

        return AnsiConsole.Confirm("Do you want to proceed with these updates?");
    }

    public void DisplayProgress(string message)
    {
        AnsiConsole.Status()
            .Start(message, ctx =>
            {
                ctx.Spinner(Spinner.Known.Star);
                ctx.SpinnerStyle(Style.Parse("green"));
                Thread.Sleep(1000); // Brief pause for visual feedback
            });
    }

    public void DisplaySuccess(string message)
    {
        AnsiConsole.MarkupLine($"[bold green]✓ {message}[/]");
    }

    public void DisplayError(string message)
    {
        AnsiConsole.MarkupLine($"[bold red]✗ {message}[/]");
    }

    public void DisplayWarning(string message)
    {
        AnsiConsole.MarkupLine($"[bold yellow]⚠ {message}[/]");
    }

    public void DisplayInfo(string message)
    {
        AnsiConsole.MarkupLine($"[cyan]ℹ {message}[/]");
    }

    public void DisplayDebug(string message)
    {
        // Only show debug messages if verbose mode is enabled
        // For now, we'll skip debug messages to avoid clutter
        // AnsiConsole.MarkupLine($"[grey]Debug: {message}[/]");
    }

    public (string username, string password) PromptForCredentials(string feedName)
    {
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"[bold yellow]Authentication required for feed: {feedName}[/]");

        var username = AnsiConsole.Ask<string>("Enter [blue]username[/]:");
        var password = AnsiConsole.Prompt(
            new TextPrompt<string>("Enter [blue]password[/]:")
                .Secret());

        return (username, password);
    }

    public string PromptForApiKey(string feedName)
    {
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"[bold yellow]API Key required for feed: {feedName}[/]");

        return AnsiConsole.Prompt(
            new TextPrompt<string>("Enter [blue]API key[/]:")
                .Secret());
    }

    public bool ConfirmSaveCredentials(string feedName)
    {
        return AnsiConsole.Confirm($"Save credentials for {feedName} to your NuGet configuration?");
    }

    public string PromptForAuthenticationType(string feedName)
    {
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"[bold]Choose authentication method for {feedName}:[/]");

        if (SupportsAnsi)
        {
            try
            {
                return AnsiConsole.Prompt(
                    new SelectionPrompt<string>()
                        .Title("Authentication method:")
                        .AddChoices(new[] {
                            "Device Flow (Browser)",
                            "Username/Password",
                            "API Key",
                            "Skip this feed"
                        }));
            }
            catch
            {
                // Fall through to simple console method
            }
        }

        // Fallback for terminals that don't support ANSI
        Console.WriteLine("Authentication method:");
        Console.WriteLine("1. Device Flow (Browser)");
        Console.WriteLine("2. Username/Password");
        Console.WriteLine("3. API Key");
        Console.WriteLine("4. Skip this feed");
        Console.Write("Enter your choice (1-4): ");

        var choice = Console.ReadLine();
        return choice switch
        {
            "1" => "Device Flow (Browser)",
            "2" => "Username/Password",
            "3" => "API Key",
            "4" => "Skip this feed",
            _ => "Skip this feed" // Default to skip if invalid input
        };
    }

    public void DisplayDeviceCodeInstructions(string userCode, string verificationUrl)
    {
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[bold yellow]Device Flow Authentication Required[/]");
        AnsiConsole.WriteLine();

        var panel = new Panel($"""
            [bold]Step 1:[/] Open your web browser and go to:
            [link blue]{verificationUrl}[/]
            
            [bold]Step 2:[/] Enter the following code:
            [bold green]{userCode}[/]
            
            [bold]Step 3:[/] Complete authentication in your browser
            """)
        {
            Header = new PanelHeader("[yellow]Browser Authentication[/]"),
            Border = BoxBorder.Rounded
        };

        AnsiConsole.Write(panel);
        AnsiConsole.WriteLine();
    }

    public void DisplayDeviceFlowProgress()
    {
        AnsiConsole.MarkupLine("[yellow]⏳ Waiting for authentication in browser...[/]");
        AnsiConsole.MarkupLine("[grey]Press Ctrl+C to cancel[/]");
    }

    public void DisplayDeviceFlowSuccess()
    {
        AnsiConsole.MarkupLine("[bold green]✓ Authentication completed successfully![/]");
    }

    public bool PromptOpenBrowser()
    {
        return AnsiConsole.Confirm("Would you like to open the verification URL in your default browser automatically?");
    }
}