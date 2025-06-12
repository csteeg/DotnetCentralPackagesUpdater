using Spectre.Console;
using CentralNuGetUpdater.Models;

namespace CentralNuGetUpdater.Services;

public class ConsoleUIService
{


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

        // Show framework information if available
        var hasFrameworkInfo = packages.Any(p => p.TargetFrameworks.Any());
        var hasVariables = packages.Any(p => !string.IsNullOrEmpty(p.OriginalVersionExpression));

        if (hasFrameworkInfo)
        {
            var allFrameworks = packages.SelectMany(p => p.TargetFrameworks).Distinct().ToList();
            AnsiConsole.MarkupLine($"[bold cyan]Target Frameworks Detected:[/] {string.Join(", ", allFrameworks)}");
        }

        if (hasVariables)
        {
            var variableCount = packages.Count(p => !string.IsNullOrEmpty(p.OriginalVersionExpression));
            AnsiConsole.MarkupLine($"[bold cyan]Variable Resolution:[/] {variableCount} packages using variables");
        }

        if (hasFrameworkInfo || hasVariables)
        {
            AnsiConsole.WriteLine();
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

            // Add target frameworks column if any package has framework information
            if (hasFrameworkInfo)
            {
                updateTable.AddColumn("Target Frameworks");
            }

            updateTable.AddColumn("Published");

            foreach (var package in packagesWithUpdates)
            {
                var currentVersion = package.CurrentVersion;
                if (!string.IsNullOrEmpty(package.OriginalVersionExpression))
                {
                    currentVersion = $"{package.CurrentVersion} [dim]({package.OriginalVersionExpression})[/]";
                }

                // Add prerelease indicator to current version
                if (package.IsCurrentVersionPrerelease)
                {
                    currentVersion = $"{currentVersion} [dim](pre)[/]";
                }

                var packageName = package.Id;
                if (package.IsGlobal)
                {
                    packageName = $"{package.Id} [dim](Global)[/]";
                }
                if (!string.IsNullOrEmpty(package.Condition))
                {
                    // Show condition for conditional packages
                    if (package.IsGlobal)
                    {
                        packageName = $"{package.Id} [dim](Global, {package.Condition})[/]";
                    }
                    else
                    {
                        packageName = $"{package.Id} [dim]({package.Condition})[/]";
                    }
                }

                var columns = new List<string>
                {
                    $"[bold]{packageName}[/]",
                    $"[red]{currentVersion}[/]",
                    $"[green]{package.LatestVersion}[/]"
                };

                // Add target frameworks if this table includes that column
                if (hasFrameworkInfo)
                {
                    var frameworks = package.ApplicableFrameworks.Any()
                        ? string.Join(", ", package.ApplicableFrameworks.Select(f => $"[dim]{f}[/]"))
                        : package.TargetFrameworks.Any()
                            ? string.Join(", ", package.TargetFrameworks.Select(f => $"[dim]{f}[/]"))
                            : "[dim]All[/]";
                    columns.Add(frameworks);
                }

                columns.Add(package.Published?.ToString("yyyy-MM-dd") ?? "N/A");

                updateTable.AddRow(columns.ToArray());
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
                var version = package.CurrentVersion;
                if (package.IsCurrentVersionPrerelease)
                {
                    version = $"{version} [dim](pre)[/]";
                }

                upToDateTable.AddRow(
                    package.Id,
                    $"[green]{version}[/]"
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
                var version = package.CurrentVersion;
                if (package.IsCurrentVersionPrerelease)
                {
                    version = $"{version} (pre)";
                }

                AnsiConsole.MarkupLine($"[red]• {package.Id} ({version})[/]");
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
        {
            var frameworks = p.ApplicableFrameworks.Any()
                ? $" [[{string.Join(", ", p.ApplicableFrameworks)}]]"  // Escape square brackets for Spectre.Console markup
                : p.TargetFrameworks.Any()
                    ? $" [[{string.Join(", ", p.TargetFrameworks)}]]"
                    : "";

            var packageName = p.Id;
            if (p.IsGlobal)
            {
                packageName = $"{p.Id} (Global)";
            }
            if (!string.IsNullOrEmpty(p.Condition))
            {
                if (p.IsGlobal)
                {
                    packageName = $"{p.Id} (Global, condition: {p.Condition})";
                }
                else
                {
                    packageName = $"{p.Id} (condition: {p.Condition})";
                }
            }

            return $"{packageName} ({p.CurrentVersion} → {p.LatestVersion}){frameworks}";
        }).ToList();

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

        try
        {
            var selectedPackages = AnsiConsole.Prompt(prompt);

            // Mark selected packages
            foreach (var package in packagesWithUpdates)
            {
                var frameworks = package.ApplicableFrameworks.Any()
                    ? $" [[{string.Join(", ", package.ApplicableFrameworks)}]]"  // Escape square brackets for Spectre.Console markup
                    : package.TargetFrameworks.Any()
                        ? $" [[{string.Join(", ", package.TargetFrameworks)}]]"
                        : "";

                var packageName = package.Id;
                if (package.IsGlobal)
                {
                    packageName = $"{package.Id} (Global)";
                }
                if (!string.IsNullOrEmpty(package.Condition))
                {
                    if (package.IsGlobal)
                    {
                        packageName = $"{package.Id} (Global, condition: {package.Condition})";
                    }
                    else
                    {
                        packageName = $"{package.Id} (condition: {package.Condition})";
                    }
                }

                var packageChoice = $"{packageName} ({package.CurrentVersion} → {package.LatestVersion}){frameworks}";
                package.IsSelected = selectedPackages.Contains(packageChoice);
            }

            return packagesWithUpdates.Where(p => p.IsSelected).ToList();
        }
        catch
        {
            // Fallback for terminals that don't support interactive selection - select all packages by default
            Console.WriteLine("Interactive selection not available - all packages will be updated:");
            for (int i = 0; i < packagesWithUpdates.Count; i++)
            {
                var package = packagesWithUpdates[i];
                var frameworks = package.ApplicableFrameworks.Any()
                    ? $" [{string.Join(", ", package.ApplicableFrameworks)}]"  // No need to escape for plain Console.WriteLine
                    : package.TargetFrameworks.Any()
                        ? $" [{string.Join(", ", package.TargetFrameworks)}]"
                        : "";

                var packageName = package.Id;
                if (package.IsGlobal)
                {
                    packageName = $"{package.Id} (Global)";
                }
                if (!string.IsNullOrEmpty(package.Condition))
                {
                    if (package.IsGlobal)
                    {
                        packageName = $"{package.Id} (Global, condition: {package.Condition})";
                    }
                    else
                    {
                        packageName = $"{package.Id} (condition: {package.Condition})";
                    }
                }

                Console.WriteLine($"{i + 1}. {packageName} ({package.CurrentVersion} → {package.LatestVersion}){frameworks}");
                package.IsSelected = true; // Select all by default
            }

            Console.WriteLine("Press Enter to continue with all packages, or Ctrl+C to cancel...");
            Console.ReadLine();

            return packagesWithUpdates;
        }
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

        // Add target frameworks column if any selected package has framework information
        var hasFrameworksInSelection = selectedPackages.Any(p => p.TargetFrameworks.Any());
        if (hasFrameworksInSelection)
        {
            summaryTable.AddColumn("Target Frameworks");
        }

        foreach (var package in selectedPackages)
        {
            var packageName = package.Id;
            if (package.IsGlobal)
            {
                packageName = $"{package.Id} [dim](Global)[/]";
            }
            if (!string.IsNullOrEmpty(package.Condition))
            {
                if (package.IsGlobal)
                {
                    packageName = $"{package.Id} [dim](Global, {package.Condition})[/]";
                }
                else
                {
                    packageName = $"{package.Id} [dim]({package.Condition})[/]";
                }
            }

            var columns = new List<string>
            {
                packageName,
                $"[red]{package.CurrentVersion}[/]",
                $"[green]{package.LatestVersion}[/]"
            };

            if (hasFrameworksInSelection)
            {
                var frameworks = package.ApplicableFrameworks.Any()
                    ? string.Join(", ", package.ApplicableFrameworks.Select(f => $"[dim]{f}[/]"))
                    : package.TargetFrameworks.Any()
                        ? string.Join(", ", package.TargetFrameworks.Select(f => $"[dim]{f}[/]"))
                        : "[dim]All[/]";
                columns.Add(frameworks);
            }

            summaryTable.AddRow(columns.ToArray());
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

    public async Task DisplayProgressWithUpdatesAsync<T>(string title, IEnumerable<T> items, Func<T, Task> action, Func<T, string>? getItemDescription = null, int maxConcurrency = 10)
    {
        var itemList = items.ToList();

        // Try to use advanced progress display, fall back to simple if needed
        try
        {
            var currentPackage = "";
            var lockObject = new object();

            await AnsiConsole.Live(new Panel($"[green]{title}[/]"))
                .StartAsync(async ctx =>
                {
                    var table = new Table().BorderStyle(Style.Parse("dim"));
                    table.AddColumn("Status");
                    table.AddColumn("Progress");

                    ctx.UpdateTarget(table);

                    var totalCount = itemList.Count;
                    var processed = 0;
                    var batchSize = Math.Min(maxConcurrency, itemList.Count);

                    for (int i = 0; i < itemList.Count; i += batchSize)
                    {
                        var batch = itemList.Skip(i).Take(batchSize);
                        var batchTasks = batch.Select(async item =>
                        {
                            try
                            {
                                // Update current package being processed
                                if (getItemDescription != null)
                                {
                                    lock (lockObject)
                                    {
                                        currentPackage = getItemDescription(item);
                                    }
                                }

                                await action(item);
                            }
                            catch (Exception ex)
                            {
                                // Log error but don't stop processing
                                AnsiConsole.WriteLine($"[red]Error processing {getItemDescription?.Invoke(item) ?? "item"}: {ex.Message}[/]");
                            }

                            var newProcessed = Interlocked.Increment(ref processed);

                            // Update the display with fixed formatting
                            lock (lockObject)
                            {
                                table.Rows.Clear();

                                var progress = newProcessed == totalCount ? 100 : (int)((double)newProcessed / totalCount * 100);
                                var progressBar = CreateProgressBar(progress);

                                if (newProcessed < totalCount && !string.IsNullOrEmpty(currentPackage))
                                {
                                    table.AddRow(
                                        $"[yellow]({newProcessed}/{totalCount})[/] [dim]{currentPackage.PadRight(30).Substring(0, Math.Min(30, currentPackage.Length))}[/]",
                                        $"{progressBar} [dim]{progress}%[/]"
                                    );
                                }
                                else
                                {
                                    table.AddRow(
                                        $"[green]✓ Complete ({newProcessed}/{totalCount})[/]",
                                        $"{progressBar} [green]{progress}%[/]"
                                    );
                                }

                                ctx.UpdateTarget(table);
                            }
                        });

                        await Task.WhenAll(batchTasks);
                    }
                });
        }
        catch
        {
            // Fallback for terminals that don't support progress bars
            for (int i = 0; i < itemList.Count; i++)
            {
                var itemDesc = getItemDescription?.Invoke(itemList[i]) ?? "";
                Console.WriteLine($"{title} ({i + 1}/{itemList.Count}) {itemDesc}...");
                await action(itemList[i]);
            }
        }
    }

    private string CreateProgressBar(int percentage, int width = 40)
    {
        var filled = (int)(width * percentage / 100.0);
        var empty = width - filled;
        return $"[green]{new string('█', filled)}[/][dim]{new string('░', empty)}[/]";
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
            // Fallback for terminals that don't support interactive selection
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