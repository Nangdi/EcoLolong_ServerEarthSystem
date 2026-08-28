using System;

// ESC 설정창의 항목 목록을 나누는 기준입니다.
public enum SettingGroup
{
    // 현장에서 전시를 운영하는 관리자가 직접 만지는 항목입니다.
    Admin = 0,
    // 설치/밸런싱 단계에서만 건드리는 항목입니다. 평소에는 접어 둡니다.
    Developer = 1,
}

// GameSettingData의 필드에 붙여 ESC 설정창에 표시할 한글 라벨과 소속 그룹을 지정합니다.
// 이 특성이 없는 필드는 필드 이름 그대로, 개발자 그룹에 표시됩니다.
[AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
public sealed class SettingFieldAttribute : Attribute
{
    // 설정창에 표시할 한글 라벨입니다.
    public string Label { get; }

    // 관리자 목록과 개발자 목록 중 어디에 표시할지 결정합니다.
    public SettingGroup Group { get; }

    // 라벨 아래에 작게 붙는 보충 설명입니다. 비워 두면 표시하지 않습니다.
    public string Description { get; }

    public SettingFieldAttribute(string label, SettingGroup group = SettingGroup.Admin, string description = "")
    {
        Label = label;
        Group = group;
        Description = description;
    }
}
