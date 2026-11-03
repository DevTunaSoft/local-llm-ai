using WorkIntelligence.Services;

if(args.Length!=3 || !Path.GetFullPath(args[1]).StartsWith(Path.Combine(Path.GetTempPath(),"wi-tests-"),StringComparison.OrdinalIgnoreCase))
    return 2; // test helper cannot operate on a normal application data root
Environment.SetEnvironmentVariable("WORKINTELLIGENCE_DATA_ROOT",args[1]);
RestoreTransaction.Checkpoint=phase=>{if(phase==args[2])Environment.Exit(91);};
await BackupService.RestoreToEmptyVaultAsync(args[0],CancellationToken.None);
return 0;
