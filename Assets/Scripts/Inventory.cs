using UnityEngine;

// 인벤토리 5칸. E 키로 열고 닫는다 (화면 아래 가운데에 표시).
// 1번 손전등(항상 가지고 있음, T로 켜고 끔), 2번 종이, 3번 샷건, 4번 소총(AR), 5번 칼.
// 지금은 가지고 있는 것을 보여 주는 표시 전용이다. 칸 선택/버리기는 나중.
// 종이는 줍는 순간 총으로 바뀌므로, 종이로 총을 받은 사람 칸에 "받음"으로 표시한다.
public class Inventory : MonoBehaviour
{
    public const int Slots = 5;

    public bool IsOpen { get; private set; }

    FirstPersonController fpc;
    GunHolder gun;
    Flashlight flash;
    GUIStyle numStyle, nameStyle;

    static readonly string[] SlotNames = { "손전등", "종이", "샷건", "소총", "칼" };

    void Update()
    {
        if (!MainMenu.IsMenuOpen && Input.GetKeyDown(KeyCode.E)) IsOpen = !IsOpen;
        if (MainMenu.IsMenuOpen) IsOpen = false;
    }

    // 이 칸을 가지고 있는가
    bool Has(int i)
    {
        switch (i)
        {
            case 0: return true;
            case 1: return gun != null && gun.GotGunFromPaper;
            case 2: return gun != null && gun.HasGun && gun.Weapon == WeaponKind.Shotgun;
            case 3: return gun != null && gun.HasGun && gun.Weapon == WeaponKind.Rifle;
            default: return gun != null && gun.HasGun && gun.Weapon == WeaponKind.Knife;
        }
    }

    string Detail(int i)
    {
        if (i == 0) return flash != null ? (flash.IsOn ? "켜짐" : "꺼짐") : "";
        if (!Has(i)) return "";
        if (i == 1) return "받음";
        return i == 4 ? "" : gun.Ammo + "/" + GunHolder.MagazineRounds;
    }

    void OnGUI()
    {
        if (!IsOpen || Event.current.type != EventType.Repaint) return;
        if (fpc == null)
        {
            fpc = FindFirstObjectByType<FirstPersonController>();
            if (fpc == null) return;
            gun = fpc.GetComponent<GunHolder>();
            flash = fpc.GetComponent<Flashlight>();
        }
        if (numStyle == null)
        {
            numStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.UpperLeft, fontStyle = FontStyle.Bold };
            nameStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
        }
        float scale = Screen.height / 1080f;
        float box = 110f * scale, gap = 10f * scale;
        float total = Slots * box + (Slots - 1) * gap;
        float x0 = (Screen.width - total) / 2f, y = Screen.height - box - 60f * scale;
        numStyle.fontSize = Mathf.RoundToInt(16f * scale);
        nameStyle.fontSize = Mathf.RoundToInt(20f * scale);

        var prev = GUI.color;
        for (int i = 0; i < Slots; i++)
        {
            var r = new Rect(x0 + i * (box + gap), y, box, box);
            bool has = Has(i);
            GUI.color = has ? new Color(0.15f, 0.15f, 0.15f, 0.85f) : new Color(0f, 0f, 0f, 0.55f);
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = has ? new Color(0.9f, 0.9f, 0.9f, 0.9f) : new Color(0.4f, 0.4f, 0.4f, 0.7f);
            GUI.DrawTexture(new Rect(r.x, r.y, r.width, 2f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.x, r.yMax - 2f, r.width, 2f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.x, r.y, 2f, r.height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.xMax - 2f, r.y, 2f, r.height), Texture2D.whiteTexture);
            GUI.color = prev;

            HudText.Label(new Rect(r.x + 6f * scale, r.y + 4f * scale, 30f * scale, 24f * scale), (i + 1).ToString(), numStyle);
            HudText.Label(r, has ? SlotNames[i] : "-", nameStyle, has ? Color.white : new Color(0.5f, 0.5f, 0.5f));
            string d = Detail(i);
            if (d.Length > 0)
                HudText.Label(new Rect(r.x, r.yMax - 30f * scale, r.width, 26f * scale), d, numStyle, new Color(0.8f, 0.9f, 0.8f));
        }
        GUI.color = prev;
    }
}
