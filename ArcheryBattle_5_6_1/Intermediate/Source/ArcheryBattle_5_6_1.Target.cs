using UnrealBuildTool;

public class ArcheryBattle_5_6_1Target : TargetRules
{
	public ArcheryBattle_5_6_1Target(TargetInfo Target) : base(Target)
	{
		DefaultBuildSettings = BuildSettingsVersion.Latest;
		IncludeOrderVersion = EngineIncludeOrderVersion.Latest;
		Type = TargetType.Game;
		ExtraModuleNames.Add("ArcheryBattle_5_6_1");
	}
}
