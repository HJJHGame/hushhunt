using UnityEngine;

// 인벤토리: 항상 화면 아래 가운데에 보인다. 가진 것만 앞에서부터 차례로 칸이 생긴다 (미리 5칸을 그리지 않음).
// 표시 순서는 고정: 종이, 칼, 권총, 소총(AR). 손전등은 항상 들고 있어서 칸에 없다(T로 켜고 끔). 얻은 순서와 상관없다. 칸 번호는 가진 것 기준으로 1부터 센다.
// 지금은 가지고 있는 것을 보여 주는 표시 전용이다. 칸 선택/버리기는 나중.
// 종이는 줍는 순간 총으로 바뀌므로, 종이로 총을 받은 사람 칸에 "받음"으로 표시한다.
public class Inventory : MonoBehaviour
{
    public const int Slots = 5;

    FirstPersonController fpc;
    GunHolder gun;
    Flashlight flash;
    GUIStyle numStyle, nameStyle;

    static readonly string[] SlotNames = { "손전등", "종이", "권총", "소총", "칼" };

    // 이 칸을 가지고 있는가
    bool Has(int i)
    {
        switch (i)
        {
            case 0: return true;
            case 1: return gun != null && gun.GotGunFromPaper;
            case 2: return gun != null && gun.HasGun && gun.Weapon == WeaponKind.Pistol;
            case 3: return gun != null && gun.HasGun && gun.Weapon == WeaponKind.Rifle;
            default: return gun != null && gun.HasGun && gun.Weapon == WeaponKind.Knife;
        }
    }

    string Detail(int i)
    {
        if (i == 0) return flash != null ? (flash.IsOn ? "켜짐" : "꺼짐") : "";
        if (!Has(i)) return "";
        if (i == 1) return "받음";
        return i == 4 ? "" : gun.Ammo + "/" + gun.MaxAmmo;
    }

    // 표시 순서(항목 번호): 종이 - 칼 - 권총 - 소총
    static readonly int[] DisplayOrder = { 1, 4, 2, 3 }; // 손전등(0)은 항상 들고 있으므로 칸에 넣지 않는다
    readonly System.Collections.Generic.List<int> owned = new System.Collections.Generic.List<int>();

    void OnGUI()
    {
        if (MainMenu.IsMenuOpen || Event.current.type != EventType.Repaint) return;
        if (fpc == null)
        {
            fpc = FindFirstObjectByType<FirstPersonController>();
            if (fpc == null) return;
            gun = fpc.GetComponent<GunHolder>();
            flash = fpc.GetComponent<Flashlight>();
        }
        if (gun != null && !gun.IsAlive) return;
        if (numStyle == null)
        {
            numStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.UpperLeft, fontStyle = FontStyle.Bold };
            nameStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
        }

        // 가진 것만 앞에서부터 차례로 채운다 (빈칸은 그리지 않는다). 새 것을 얻으면 다음 칸이 생긴다.
        owned.Clear();
        foreach (int i in DisplayOrder) if (Has(i)) owned.Add(i); // 얻은 순서와 상관없이 고정 순서

        if (owned.Count == 0) return;
        float scale = Screen.height / 1080f;
        float box = 96f * scale, gap = 8f * scale;
        float total = owned.Count * box + (owned.Count - 1) * gap;
        float x0 = (Screen.width - total) / 2f, y = Screen.height - box - 24f * scale;
        numStyle.fontSize = Mathf.RoundToInt(15f * scale);
        nameStyle.fontSize = Mathf.RoundToInt(19f * scale);

        var prev = GUI.color;
        for (int k = 0; k < owned.Count; k++)
        {
            int i = owned[k];
            var r = new Rect(x0 + k * (box + gap), y, box, box);
            UiTheme.Panel(r, 0.62f, 0.5f);
            GUI.color = prev;

            HudText.Label(new Rect(r.x + 6f * scale, r.y + 4f * scale, 30f * scale, 24f * scale), (k + 1).ToString(), numStyle);
            HudText.Label(r, SlotNames[i], nameStyle, Color.white);
            string d = Detail(i);
            if (d.Length > 0)
                HudText.Label(new Rect(r.x, r.yMax - 28f * scale, r.width, 24f * scale), d, numStyle, new Color(0.8f, 0.9f, 0.8f));
        }
        GUI.color = prev;
    }
}
