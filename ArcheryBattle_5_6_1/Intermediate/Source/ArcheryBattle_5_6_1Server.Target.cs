using UnrealBuildTool;

public class ArcheryBattle_5_6_1ServerTarget : TargetRules
{
	public ArcheryBattle_5_6_1ServerTarget(TargetInfo Target) : base(Target)
	{
		DefaultBuildSettings = BuildSettingsVersion.Latest;
		IncludeOrderVersion = EngineIncludeOrderVersion.Latest;
		Type = TargetType.Server;
		ExtraModuleNames.Add("ArcheryBattle_5_6_1");
	}
}
