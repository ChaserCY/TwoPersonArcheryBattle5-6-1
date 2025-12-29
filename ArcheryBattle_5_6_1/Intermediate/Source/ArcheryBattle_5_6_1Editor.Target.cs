using UnrealBuildTool;

public class ArcheryBattle_5_6_1EditorTarget : TargetRules
{
	public ArcheryBattle_5_6_1EditorTarget(TargetInfo Target) : base(Target)
	{
		DefaultBuildSettings = BuildSettingsVersion.Latest;
		IncludeOrderVersion = EngineIncludeOrderVersion.Latest;
		Type = TargetType.Editor;
		ExtraModuleNames.Add("ArcheryBattle_5_6_1");
	}
}
