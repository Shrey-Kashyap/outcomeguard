# Run OutcomeGuard

Install the [ASP.NET Core Runtime 10](https://dotnet.microsoft.com/download/dotnet/10.0) for your OS. A .NET 10 SDK also works. Extract the entire archive before starting the app.

From the extracted folder, run `./Start-OutcomeGuard.ps1` in PowerShell on Windows or `sh start-outcomeguard.sh` on macOS or Linux.

Open **http://127.0.0.1:5080** and choose **Demo lab**. Run a scenario and open its incident. Try **Verify recovery**, use **Simulate recovery** to add synthetic evidence, then verify again.

The app listens on your own machine. SQLite saves your work to the `data/` directory alongside the launcher. Press Ctrl+C to stop it.

See the [source and full setup instructions](https://github.com/Shrey-Kashyap/outcomeguard) and the [demo walkthrough](https://github.com/Shrey-Kashyap/outcomeguard/blob/main/docs/demo-guide.md).
