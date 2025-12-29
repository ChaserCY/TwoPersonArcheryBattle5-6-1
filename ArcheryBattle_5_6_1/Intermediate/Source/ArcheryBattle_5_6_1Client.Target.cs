using UnrealBuildTool;

public class ArcheryBattle_5_6_1ClientTarget : TargetRules
{
	public ArcheryBattle_5_6_1ClientTarget(TargetInfo Target) : base(Target)
	{
		DefaultBuildSettings = BuildSettingsVersion.Latest;
		IncludeOrderVersion = EngineIncludeOrderVersion.Latest;
		Type = TargetType.Client;
		ExtraModuleNames.Add("ArcheryBattle_5_6_1");
	}
}
