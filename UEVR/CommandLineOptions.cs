namespace UEVR
{
	class CommandLineOptions
{
    public string ExecutablePath { get; private set; }
    public string Arguments { get; private set; }
    public bool RunOnStartup { get; private set; }

    public CommandLineOptions(string[] args)
    {
        ParseArguments(args);
    }

    private void ParseArguments(string[] args)
    {
        foreach (string arg in args)
        {
            if (arg.StartsWith("--attach="))
            {
                ExecutablePath = arg.Split('=')[1].Trim('"');
            }
            else if (arg.StartsWith("--runAndAttach="))
            {
                ExecutablePath = arg.Split('=')[1].Trim('"');
                RunOnStartup = true;
            }
            else if (arg.StartsWith("--arguments="))
            {
                Arguments = arg.Split('=')[1].Trim('"');
            }
        }
    }
}

}