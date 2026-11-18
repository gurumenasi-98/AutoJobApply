using Microsoft.Playwright;
using Microsoft.Extensions.Configuration;

class Program
{
    static async Task Main(string[] args)
    {
        IConfiguration config = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false)
            .Build();

        string username = config["Naukri:Username"]!;
        string password = config["Naukri:Password"]!;

        Console.WriteLine("=========================================");
        Console.WriteLine("Starting Naukri automation...");
        Console.WriteLine("=========================================");

        using var playwright = await Playwright.CreateAsync();

        var browser = await playwright.Chromium.LaunchAsync(
            new BrowserTypeLaunchOptions
            {
                Headless = false,
                Channel = "msedge"
            });

        var context = await browser.NewContextAsync();
        var page = await context.NewPageAsync();

        page.SetDefaultTimeout(15000);

        // =========================================================
        // OPEN NAUKRI
        // =========================================================

        Console.WriteLine("Opening Naukri...");

        await page.GotoAsync(
            "https://www.naukri.com/",
            new PageGotoOptions
            {
                WaitUntil = WaitUntilState.DOMContentLoaded
            });

        Console.WriteLine("Naukri opened.");

        // =========================================================
        // LOGIN
        // =========================================================

        try
        {
            Console.WriteLine("Opening Login...");

            await page.GetByText("Login", new PageGetByTextOptions
            {
                Exact = true
            }).ClickAsync();

            await page.WaitForTimeoutAsync(2000);

            var usernameBox = page.Locator(
                "input[placeholder*='Email'], " +
                "input[placeholder*='Username'], " +
                "input[type='text']"
            ).First;

            await usernameBox.FillAsync(username);

            var passwordBox = page.Locator("input[type='password']").First;

            await passwordBox.FillAsync(password);

            await page.GetByRole(AriaRole.Button, new PageGetByRoleOptions
            {
                Name = "Login",
                Exact = true
            }).ClickAsync();

            await page.WaitForTimeoutAsync(5000);

            Console.WriteLine("Login completed.");
        }
        catch (Exception ex)
        {
            Console.WriteLine("Login failed.");
            Console.WriteLine(ex.Message);

            await browser.CloseAsync();
            return;
        }

        // =========================================================
        // OPEN JOB SEARCH
        // =========================================================

        try
        {
            Console.WriteLine("Opening job search...");

            var searchJobsButton = page.Locator(
                "button[aria-label='Search jobs here']"
            );

            await searchJobsButton.ClickAsync();

            await page.WaitForTimeoutAsync(1000);
        }
        catch (Exception ex)
        {
            Console.WriteLine("Could not open job search.");
            Console.WriteLine(ex.Message);

            await browser.CloseAsync();
            return;
        }

        // =========================================================
        // SEARCH PARAMETERS
        // =========================================================

        string keyword =
            ".NET, .NET Core, ASP.NET Core, ASP.NET Web API, C#, " +
            "React.js, SQL Server, REST API, EF Core, LINQ, " +
            "JavaScript, Middleware, Dependency Injection, Git, Swagger";

        string location = "Bengaluru";

        // =========================================================
        // ENTER KEYWORD
        // =========================================================

        try
        {
            var searchBox = page.Locator(
                "input[placeholder='Enter keyword / designation / companies']"
            );

            await searchBox.FillAsync(keyword);

            // =====================================================
            // EXPERIENCE
            // =====================================================

            var experienceBox = page.Locator(
                "input[name='experienceDD']"
            );

            await experienceBox.ClickAsync();

            await page.GetByText("4 years", new PageGetByTextOptions
            {
                Exact = true
            }).ClickAsync();

            // =====================================================
            // LOCATION
            // =====================================================
            var locationBox = page.Locator(
                "input[placeholder='Enter location']"
            );

            await locationBox.FillAsync(location);

            await page.WaitForTimeoutAsync(1500);

            // Select the first visible Bengaluru suggestion
            var bengaluruSuggestions = page.Locator(
                "div[class*='suggest']"
            ).Locator("text=Bengaluru");

            if (await bengaluruSuggestions.CountAsync() > 0)
            {
                await bengaluruSuggestions.First.ClickAsync();
            }
            else
            {
                // Fallback: press ArrowDown + Enter
                await locationBox.PressAsync("ArrowDown");
                await locationBox.PressAsync("Enter");
            }

            await page.WaitForTimeoutAsync(1000);

            Console.WriteLine("Location selected: Bengaluru");

            // =====================================================
            // SEARCH
            // =====================================================

            await page.GetByRole(AriaRole.Button, new PageGetByRoleOptions
            {
                Name = "Search",
                Exact = true
            }).ClickAsync();

            await page.WaitForTimeoutAsync(5000);

            Console.WriteLine("Job search completed.");
        }
        catch (Exception ex)
        {
            Console.WriteLine("Job search failed.");
            Console.WriteLine(ex.Message);

            await browser.CloseAsync();
            return;
        }

        // =========================================================
        // SORT BY DATE
        // =========================================================

        try
        {
            await page.Locator("button#filter-sort").ClickAsync();

            await page.Locator("a[data-id='filter-sort-f']").ClickAsync();

            await page.WaitForTimeoutAsync(3000);

            Console.WriteLine("Sorted by date.");
        }
        catch (Exception ex)
        {
            Console.WriteLine("Could not apply Date sorting.");
            Console.WriteLine(ex.Message);
        }

        // =========================================================
        // FRESHNESS - LAST 7 DAYS
        // =========================================================

        try
        {
            await page.Locator("button#filter-freshness").ClickAsync();

            await page.Locator("a[data-id='filter-freshness-7']").ClickAsync();

            await page.WaitForTimeoutAsync(3000);

            Console.WriteLine("Freshness filter: Last 7 days.");
        }
        catch (Exception ex)
        {
            Console.WriteLine("Could not apply freshness filter.");
            Console.WriteLine(ex.Message);
        }

        // =========================================================
        // GET JOB CARDS
        // =========================================================

        var jobCards = page.Locator(
            "div[class*='jobTuple'], " +
            "div[class*='job-tuple'], " +
            "article"
        );

        int totalJobs = await jobCards.CountAsync();

        Console.WriteLine();
        Console.WriteLine("Total job cards found: " + totalJobs);

        if (totalJobs == 0)
        {
            Console.WriteLine("No jobs found.");
            await browser.CloseAsync();
            return;
        }

        // =========================================================
        // KEYWORDS
        // =========================================================

        string[] backendKeywords =
        {
            ".NET",
            ".NET Core",
            "ASP.NET Core",
            "ASP.NET Web API",
            "C#"
        };

        string[] supportingKeywords =
        {
            "React.js",
            "React",
            "Angular",
            "SQL Server",
            "REST API",
            "EF Core",
            "Entity Framework",
            "LINQ",
            "JavaScript",
            "Middleware",
            "Dependency Injection",
            "Git",
            "Swagger"
        };

        var relevantJobs = new List<JobInfo>();

        // =========================================================
        // FILTER JOBS
        // =========================================================

        for (int i = 0; i < totalJobs; i++)
        {
            try
            {
                var card = jobCards.Nth(i);

                if (!await card.IsVisibleAsync())
                    continue;

                string jobText = await card.InnerTextAsync();

                var matchedBackend = backendKeywords
                    .Where(k =>
                        jobText.Contains(
                            k,
                            StringComparison.OrdinalIgnoreCase))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                var matchedSupporting = supportingKeywords
                    .Where(k =>
                        jobText.Contains(
                            k,
                            StringComparison.OrdinalIgnoreCase))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                var allMatchedKeywords = matchedBackend
                    .Concat(matchedSupporting)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                // Must have:
                // 1 backend keyword
                // AND
                // 1 supporting keyword

                if (matchedBackend.Count >= 1 &&
                    matchedSupporting.Count >= 1)
                {
                    string jobTitle = "Unknown Job";
                    string jobUrl = "";

                    var links = card.Locator("a");

                    int linkCount = await links.CountAsync();

                    for (int j = 0; j < linkCount; j++)
                    {
                        var link = links.Nth(j);

                        string? href =
                            await link.GetAttributeAsync("href");

                        if (string.IsNullOrWhiteSpace(href))
                            continue;

                        if (href.Contains(
                                "/job-listings/",
                                StringComparison.OrdinalIgnoreCase) ||
                            href.Contains(
                                "/job/",
                                StringComparison.OrdinalIgnoreCase))
                        {
                            jobUrl = href;

                            string linkText =
                                (await link.InnerTextAsync()).Trim();

                            if (!string.IsNullOrWhiteSpace(linkText))
                                jobTitle = linkText;

                            break;
                        }
                    }

                    if (string.IsNullOrWhiteSpace(jobUrl))
                    {
                        try
                        {
                            var firstLink = links.First;

                            jobUrl =
                                await firstLink.GetAttributeAsync("href")
                                ?? "";

                            string linkText =
                                (await firstLink.InnerTextAsync()).Trim();

                            if (!string.IsNullOrWhiteSpace(linkText))
                                jobTitle = linkText;
                        }
                        catch
                        {
                        }
                    }

                    if (!string.IsNullOrWhiteSpace(jobUrl))
                    {
                        relevantJobs.Add(
                            new JobInfo
                            {
                                Index = i + 1,
                                Title = jobTitle,
                                Url = jobUrl,
                                JobText = jobText,
                                MatchedKeywords = allMatchedKeywords
                            });
                    }
                }
                else
                {
                    Console.WriteLine(
                        $"Job {i + 1}: Not relevant - skipped.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    $"Error reading JOB {i + 1}: {ex.Message}");
            }
        }

        // =========================================================
        // SUMMARY
        // =========================================================

        Console.WriteLine();
        Console.WriteLine("=========================================");
        Console.WriteLine("RELEVANT JOBS FOUND: " + relevantJobs.Count);
        Console.WriteLine("=========================================");

        foreach (var job in relevantJobs)
        {
            Console.WriteLine();
            Console.WriteLine($"JOB {job.Index}: {job.Title}");
            Console.WriteLine(
                "Matched: " +
                string.Join(", ", job.MatchedKeywords));
        }

        // =========================================================
        // PROCESS EACH JOB
        // =========================================================

        foreach (var job in relevantJobs)
        {
            IPage? jobPage = null;

            try
            {
                Console.WriteLine();
                Console.WriteLine("-----------------------------------------");
                Console.WriteLine("Opening: " + job.Title);
                Console.WriteLine("-----------------------------------------");

                jobPage = await context.NewPageAsync();

                jobPage.SetDefaultTimeout(15000);

                string absoluteUrl =
                    MakeAbsoluteUrl(job.Url);

                await jobPage.GotoAsync(
                    absoluteUrl,
                    new PageGotoOptions
                    {
                        WaitUntil =
                            WaitUntilState.DOMContentLoaded,

                        Timeout = 30000
                    });

                Console.WriteLine("Job page loaded.");

                await jobPage.WaitForTimeoutAsync(2000);

                try
                {
                    Console.WriteLine(
                        "Page title: " +
                        await jobPage.TitleAsync());
                }
                catch
                {
                    Console.WriteLine(
                        "Could not read page title.");
                }

                if (jobPage.IsClosed)
                {
                    Console.WriteLine(
                        "Job page closed unexpectedly.");

                    continue;
                }

                // =================================================
                // APPLY BUTTON
                // =================================================

                Console.WriteLine("Checking Apply button...");

                var applyButtons = jobPage.Locator(
                    "button:has-text('Apply'), " +
                    "a:has-text('Apply'), " +
                    "[role='button']:has-text('Apply')"
                );

                int applyCount =
                    await applyButtons.CountAsync();

                Console.WriteLine(
                    "Possible Apply elements found: " +
                    applyCount);

                if (applyCount == 0)
                {
                    Console.WriteLine(
                        "Apply button not found.");

                    continue;
                }

                ILocator? visibleApplyButton = null;

                for (int a = 0; a < applyCount; a++)
                {
                    var current =
                        applyButtons.Nth(a);

                    try
                    {
                        if (await current.IsVisibleAsync())
                        {
                            visibleApplyButton = current;
                            break;
                        }
                    }
                    catch
                    {
                    }
                }

                if (visibleApplyButton == null)
                {
                    Console.WriteLine(
                        "No visible Apply button found.");

                    continue;
                }

                Console.WriteLine("Apply button found.");

                await visibleApplyButton.ClickAsync(
                    new LocatorClickOptions
                    {
                        Timeout = 15000
                    });

                Console.WriteLine("Apply clicked.");

                await jobPage.WaitForTimeoutAsync(3000);

                // =================================================
                // CHECK APPLICATION QUESTIONS
                // =================================================

                await HandleApplicationQuestionsAsync(
                    jobPage);

                // =================================================
                // CHECK RESULT
                // =================================================

                await jobPage.WaitForTimeoutAsync(2000);

                if (jobPage.IsClosed)
                {
                    Console.WriteLine(
                        "Job page closed after Apply.");

                    continue;
                }

                string afterApplyText = "";

                try
                {
                    afterApplyText =
                        await jobPage
                            .Locator("body")
                            .InnerTextAsync();
                }
                catch
                {
                    Console.WriteLine(
                        "Could not read page after Apply.");

                    continue;
                }

                if (afterApplyText.Contains(
                        "Already Applied",
                        StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine(
                        "Already applied to this job.");
                }
                else if (afterApplyText.Contains(
                             "Application submitted",
                             StringComparison.OrdinalIgnoreCase) ||
                         afterApplyText.Contains(
                             "Applied",
                             StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine(
                        "Application appears to be completed.");
                }
                else
                {
                    Console.WriteLine(
                        "Apply was clicked.");

                    Console.WriteLine(
                        "Further application steps may be required.");
                }
            }
            catch (PlaywrightException ex)
            {
                Console.WriteLine(
                    "Playwright error while processing job:");

                Console.WriteLine(ex.Message);
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    "Unexpected error while processing job:");

                Console.WriteLine(ex.Message);
            }
            finally
            {
                if (jobPage != null)
                {
                    try
                    {
                        if (!jobPage.IsClosed)
                            await jobPage.CloseAsync();

                        Console.WriteLine(
                            "Job tab closed.");
                    }
                    catch (Exception closeEx)
                    {
                        Console.WriteLine(
                            "Could not close job tab: " +
                            closeEx.Message);
                    }
                }

                await Task.Delay(1000);
            }
        }

        // =========================================================
        // COMPLETED
        // =========================================================

        Console.WriteLine();
        Console.WriteLine("=========================================");
        Console.WriteLine("AUTOMATION COMPLETED");
        Console.WriteLine("=========================================");

        Console.WriteLine(
            "Relevant jobs processed: " +
            relevantJobs.Count);

        Console.WriteLine();
        Console.WriteLine("Automation completed.");
        await browser.CloseAsync();
    }

    // =============================================================
    // APPLICATION QUESTION HANDLER
    // =============================================================

    static async Task HandleApplicationQuestionsAsync(
        IPage page)
    {
        try
        {
            Console.WriteLine();
            Console.WriteLine(
                "Checking application questions...");

            // Give Naukri time to display the form.
            await page.WaitForTimeoutAsync(2000);

            // -----------------------------------------------------
            // TEXT INPUTS
            // -----------------------------------------------------

            var inputs = page.Locator(
                "input:not([type='hidden'])");

            int inputCount =
                await inputs.CountAsync();

            for (int i = 0; i < inputCount; i++)
            {
                try
                {
                    var input = inputs.Nth(i);

                    if (!await input.IsVisibleAsync())
                        continue;

                    string type =
                        (await input.GetAttributeAsync("type")
                         ?? "text").ToLower();

                    if (type == "checkbox" ||
                        type == "radio" ||
                        type == "file" ||
                        type == "submit" ||
                        type == "button")
                    {
                        continue;
                    }

                    string placeholder =
                        await input.GetAttributeAsync(
                            "placeholder") ?? "";

                    string name =
                        await input.GetAttributeAsync(
                            "name") ?? "";

                    string ariaLabel =
                        await input.GetAttributeAsync(
                            "aria-label") ?? "";

                    string question =
                        $"{placeholder} {name} {ariaLabel}"
                        .Trim();

                    if (string.IsNullOrWhiteSpace(question))
                        continue;

                    string answer =
                        GetAnswerForQuestion(question);

                    if (!string.IsNullOrWhiteSpace(answer))
                    {
                        await input.FillAsync(answer);

                        Console.WriteLine(
                            $"Answered: {question} -> {answer}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(
                        "Could not process input: " +
                        ex.Message);
                }
            }

            // -----------------------------------------------------
            // TEXTAREAS
            // -----------------------------------------------------

            var textareas =
                page.Locator("textarea");

            int textareaCount =
                await textareas.CountAsync();

            for (int i = 0; i < textareaCount; i++)
            {
                try
                {
                    var textarea =
                        textareas.Nth(i);

                    if (!await textarea.IsVisibleAsync())
                        continue;

                    string placeholder =
                        await textarea.GetAttributeAsync(
                            "placeholder") ?? "";

                    string name =
                        await textarea.GetAttributeAsync(
                            "name") ?? "";

                    string question =
                        $"{placeholder} {name}".Trim();

                    if (question.Contains(
                            "reason",
                            StringComparison.OrdinalIgnoreCase))
                    {
                        await textarea.FillAsync(
                            "Career growth and better opportunities");

                        Console.WriteLine(
                            "Answered reason for job change.");
                    }
                }
                catch
                {
                }
            }

            // -----------------------------------------------------
            // SELECT / DROPDOWN
            // -----------------------------------------------------

            var selects =
                page.Locator("select");

            int selectCount =
                await selects.CountAsync();

            for (int i = 0; i < selectCount; i++)
            {
                try
                {
                    var select =
                        selects.Nth(i);

                    if (!await select.IsVisibleAsync())
                        continue;

                    string name =
                        await select.GetAttributeAsync(
                            "name") ?? "";

                    string ariaLabel =
                        await select.GetAttributeAsync(
                            "aria-label") ?? "";

                    string question =
                        $"{name} {ariaLabel}";

                    await HandleSelectAsync(
                        select,
                        question);
                }
                catch
                {
                }
            }

            // -----------------------------------------------------
            // RADIO BUTTONS
            // -----------------------------------------------------

            await HandleRadioQuestionsAsync(page);

            // -----------------------------------------------------
            // CHECKBOXES
            // -----------------------------------------------------

            await HandleCheckboxesAsync(page);

            // -----------------------------------------------------
            // SUBMIT / NEXT
            // -----------------------------------------------------

            Console.WriteLine(
                "Checking for Next / Submit button...");

            var buttons = page.Locator(
                "button, input[type='submit'], " +
                "[role='button']");

            int buttonCount =
                await buttons.CountAsync();

            for (int i = 0; i < buttonCount; i++)
            {
                try
                {
                    var button =
                        buttons.Nth(i);

                    if (!await button.IsVisibleAsync())
                        continue;

                    string text =
                        (await button.InnerTextAsync()).Trim();

                    string value =
                        await button.GetAttributeAsync(
                            "value") ?? "";

                    string buttonText =
                        $"{text} {value}".Trim();

                    if (buttonText.Equals(
                            "Submit",
                            StringComparison.OrdinalIgnoreCase) ||
                        buttonText.Equals(
                            "Submit Application",
                            StringComparison.OrdinalIgnoreCase))
                    {
                        Console.WriteLine(
                            "Submit button found.");

                        await button.ClickAsync();

                        await page.WaitForTimeoutAsync(3000);

                        Console.WriteLine(
                            "Submit clicked.");

                        break;
                    }

                    if (buttonText.Equals(
                            "Next",
                            StringComparison.OrdinalIgnoreCase) ||
                        buttonText.Equals(
                            "Continue",
                            StringComparison.OrdinalIgnoreCase))
                    {
                        Console.WriteLine(
                            "Next/Continue button found.");

                        await button.ClickAsync();

                        await page.WaitForTimeoutAsync(2000);

                        Console.WriteLine(
                            "Next/Continue clicked.");

                        // Re-check questions after moving
                        // to the next application page.
                        await HandleApplicationQuestionsAsync(
                            page);

                        break;
                    }
                }
                catch
                {
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(
                "Application question handling error:");

            Console.WriteLine(ex.Message);
        }
    }

    // =============================================================
    // ANSWER MAPPING
    // =============================================================

    static string GetAnswerForQuestion(
        string question)
    {
        string q =
            question.ToLowerInvariant();

        // -----------------------------------------
        // REACT
        // -----------------------------------------

        if (q.Contains("react"))
            return "2 years 4 months";

        // -----------------------------------------
        // TOTAL EXPERIENCE
        // -----------------------------------------

        if (q.Contains("total experience") ||
            q.Contains("overall experience"))
            return "3 years 9 months";

        // -----------------------------------------
        // .NET
        // -----------------------------------------

        if (q.Contains(".net") ||
            q.Contains("dot net") ||
            q.Contains("c#") ||
            q.Contains("csharp"))
            return "3 years 9 months";

        // -----------------------------------------
        // ASP.NET
        // -----------------------------------------

        if (q.Contains("asp.net") ||
            q.Contains("asp net") ||
            q.Contains("web api"))
            return "3 years 9 months";

        // -----------------------------------------
        // SQL
        // -----------------------------------------

        if (q.Contains("sql server") ||
            q.Contains("database"))
            return "3 years 9 months";

        // -----------------------------------------
        // ENTITY FRAMEWORK
        // -----------------------------------------

        if (q.Contains("entity framework") ||
            q.Contains("ef core"))
            return "3 years 9 months";

        // -----------------------------------------
        // LINQ
        // -----------------------------------------

        if (q.Contains("linq"))
            return "3 years 9 months";

        // -----------------------------------------
        // JAVASCRIPT
        // -----------------------------------------

        if (q.Contains("javascript"))
            return "3 years 9 months";

        // -----------------------------------------
        // HTML / CSS
        // -----------------------------------------

        if (q.Contains("html") ||
            q.Contains("css"))
            return "3 years 9 months";

        // -----------------------------------------
        // REST API
        // -----------------------------------------

        if (q.Contains("rest api") ||
            q.Contains("restful"))
            return "3 years 9 months";

        // -----------------------------------------
        // GIT
        // -----------------------------------------

        if (q.Contains("git"))
            return "3 years 9 months";

        // -----------------------------------------
        // SWAGGER
        // -----------------------------------------

        if (q.Contains("swagger"))
            return "3 years 9 months";

        // -----------------------------------------
        // NOTICE PERIOD
        // -----------------------------------------

        if (q.Contains("notice period") ||
            q.Contains("notice"))
            return "15 days";

        // -----------------------------------------
        // CURRENT CTC
        // -----------------------------------------

        if (q.Contains("current ctc") ||
            q.Contains("current salary") ||
            q.Contains("current compensation"))
            return "6.3";

        // -----------------------------------------
        // EXPECTED CTC
        // -----------------------------------------

        if (q.Contains("expected ctc") ||
            q.Contains("expected salary") ||
            q.Contains("expected compensation"))
            return "12";

        // -----------------------------------------
        // CURRENT LOCATION
        // -----------------------------------------

        if (q.Contains("current location") ||
            q.Contains("location"))
            return "Bengaluru";

        // -----------------------------------------
        // RELOCATION
        // -----------------------------------------

        if (q.Contains("relocate") ||
            q.Contains("relocation"))
            return "Yes";

        // -----------------------------------------
        // WORK FROM OFFICE
        // -----------------------------------------

        if (q.Contains("work from office") ||
            q.Contains("office"))
            return "Yes";

        // -----------------------------------------
        // HYBRID
        // -----------------------------------------

        if (q.Contains("hybrid"))
            return "Yes";

        // -----------------------------------------
        // ONSITE
        // -----------------------------------------

        if (q.Contains("onsite") ||
            q.Contains("on-site"))
            return "Yes";

        // -----------------------------------------
        // TRAVEL
        // -----------------------------------------

        if (q.Contains("travel"))
            return "Yes";

        // -----------------------------------------
        // SHIFT
        // -----------------------------------------

        if (q.Contains("shift"))
            return "Yes";

        // -----------------------------------------
        // WEEKEND
        // -----------------------------------------

        if (q.Contains("weekend"))
            return "Yes";

        // -----------------------------------------
        // CURRENTLY WORKING
        // -----------------------------------------

        if (q.Contains("currently employed") ||
            q.Contains("currently working") ||
            q.Contains("employed"))
            return "Yes";

        // -----------------------------------------
        // SPONSORSHIP
        // -----------------------------------------

        if (q.Contains("sponsorship"))
            return "No";

        // -----------------------------------------
        // WORK AUTHORIZATION
        // -----------------------------------------

        if (q.Contains("authorized to work") ||
            q.Contains("work authorization"))
            return "Yes";

        // -----------------------------------------
        // JOINING
        // -----------------------------------------

        if (q.Contains("immediate joiner") ||
            q.Contains("join immediately"))
            return "Yes";

        // -----------------------------------------
        // BUYOUT
        // -----------------------------------------

        if (q.Contains("buyout"))
            return "Yes";

        // -----------------------------------------
        // REASON FOR CHANGE
        // -----------------------------------------

        if (q.Contains("reason for change") ||
            q.Contains("reason for leaving") ||
            q.Contains("reason for job change"))
        {
            return "Career growth and better opportunities";
        }

        // -----------------------------------------
        // UNKNOWN
        // -----------------------------------------

        return "";
    }

    // =============================================================
    // SELECT HANDLER
    // =============================================================

    static async Task HandleSelectAsync(
        ILocator select,
        string question)
    {
        string q =
            question.ToLowerInvariant();

        try
        {
            if (q.Contains("relocat"))
            {
                await SelectYesNoAsync(select, true);
            }
            else if (q.Contains("office") ||
                     q.Contains("hybrid") ||
                     q.Contains("onsite"))
            {
                await SelectYesNoAsync(select, true);
            }
            else if (q.Contains("travel"))
            {
                await SelectYesNoAsync(select, true);
            }
            else if (q.Contains("shift"))
            {
                await SelectYesNoAsync(select, true);
            }
            else if (q.Contains("notice"))
            {
                await SelectOptionContainingAsync(
                    select,
                    "15");
            }
        }
        catch
        {
        }
    }

    // =============================================================
    // YES / NO SELECT
    // =============================================================

    static async Task SelectYesNoAsync(
        ILocator select,
        bool yes)
    {
        var options =
            select.Locator("option");

        int count =
            await options.CountAsync();

        for (int i = 0; i < count; i++)
        {
            var option =
                options.Nth(i);

            string text =
                (await option.InnerTextAsync()).Trim();

            if (yes &&
                text.Equals(
                    "Yes",
                    StringComparison.OrdinalIgnoreCase))
            {
                string? value =
                    await option.GetAttributeAsync("value");

                if (!string.IsNullOrWhiteSpace(value))
                    await select.SelectOptionAsync(value);

                return;
            }
        }
    }

    // =============================================================
    // SELECT OPTION CONTAINING TEXT
    // =============================================================

    static async Task SelectOptionContainingAsync(
        ILocator select,
        string searchText)
    {
        var options =
            select.Locator("option");

        int count =
            await options.CountAsync();

        for (int i = 0; i < count; i++)
        {
            var option =
                options.Nth(i);

            string text =
                (await option.InnerTextAsync()).Trim();

            if (text.Contains(
                    searchText,
                    StringComparison.OrdinalIgnoreCase))
            {
                string? value =
                    await option.GetAttributeAsync("value");

                if (!string.IsNullOrWhiteSpace(value))
                    await select.SelectOptionAsync(value);

                return;
            }
        }
    }

    // =============================================================
    // RADIO QUESTIONS
    // =============================================================

    static async Task HandleRadioQuestionsAsync(
        IPage page)
    {
        var radios =
            page.Locator("input[type='radio']");

        int count =
            await radios.CountAsync();

        for (int i = 0; i < count; i++)
        {
            try
            {
                var radio =
                    radios.Nth(i);

                if (!await radio.IsVisibleAsync())
                    continue;

                string value =
                    await radio.GetAttributeAsync(
                        "value") ?? "";

                string ariaLabel =
                    await radio.GetAttributeAsync(
                        "aria-label") ?? "";

                string text =
                    $"{value} {ariaLabel}";

                // We only automatically select
                // obvious Yes options.

                if (text.Equals(
                        "yes",
                        StringComparison.OrdinalIgnoreCase) ||
                    text.Contains(
                        "yes",
                        StringComparison.OrdinalIgnoreCase))
                {
                    await radio.CheckAsync();

                    Console.WriteLine(
                        "Selected Yes radio button.");

                    // Do not blindly select every Yes
                    // radio on the page.
                    break;
                }
            }
            catch
            {
            }
        }
    }

    // =============================================================
    // CHECKBOXES
    // =============================================================

    static async Task HandleCheckboxesAsync(
        IPage page)
    {
        var checkboxes =
            page.Locator(
                "input[type='checkbox']");

        int count =
            await checkboxes.CountAsync();

        for (int i = 0; i < count; i++)
        {
            try
            {
                var checkbox =
                    checkboxes.Nth(i);

                if (!await checkbox.IsVisibleAsync())
                    continue;

                string ariaLabel =
                    await checkbox.GetAttributeAsync(
                        "aria-label") ?? "";

                string value =
                    await checkbox.GetAttributeAsync(
                        "value") ?? "";

                string text =
                    $"{ariaLabel} {value}";

                // Only check boxes that clearly
                // indicate agreement/consent.

                if (text.Contains(
                        "agree",
                        StringComparison.OrdinalIgnoreCase) ||
                    text.Contains(
                        "consent",
                        StringComparison.OrdinalIgnoreCase))
                {
                    if (!await checkbox.IsCheckedAsync())
                        await checkbox.CheckAsync();

                    Console.WriteLine(
                        "Agreement/consent checkbox selected.");
                }
            }
            catch
            {
            }
        }
    }

    // =============================================================
    // ABSOLUTE URL
    // =============================================================

    static string MakeAbsoluteUrl(string url)
    {
        if (url.StartsWith(
                "http://",
                StringComparison.OrdinalIgnoreCase) ||
            url.StartsWith(
                "https://",
                StringComparison.OrdinalIgnoreCase))
        {
            return url;
        }

        if (url.StartsWith("/"))
            return "https://www.naukri.com" + url;

        return "https://www.naukri.com/" + url;
    }
}

// ================================================================
// JOB MODEL
// ================================================================

class JobInfo
{
    public int Index { get; set; }

    public string Title { get; set; } = "";

    public string Url { get; set; } = "";

    public string JobText { get; set; } = "";

    public List<string> MatchedKeywords { get; set; }
        = new List<string>();
}