using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 정적 아이콘 바인더. GlassUIKit의 절차적 아이콘 스프라이트는 런타임 생성 객체라 씬에 저장되지 않으므로,
/// 이 컴포넌트가 Awake 시점에 같은 오브젝트의 Image.sprite를 채운다(에디터 편집 화면에선 비어 보이고, Play에서 채워짐).
/// </summary>
[RequireComponent(typeof(Image))]
public class GlassIcon : MonoBehaviour
{
    public enum Kind { Breath, Paw, Heart, Check, Chevron }

    [SerializeField] private Kind kind = Kind.Breath;

    private void Awake()
    {
        var img = GetComponent<Image>();
        if (img != null) img.sprite = Resolve(kind);
    }

    public void SetKind(Kind k)
    {
        kind = k;
        var img = GetComponent<Image>();
        if (img != null) img.sprite = Resolve(k);
    }

    public static Sprite Resolve(Kind k)
    {
        switch (k)
        {
            case Kind.Paw: return GlassUIKit.IconPaw;
            case Kind.Heart: return GlassUIKit.IconHeart;
            case Kind.Check: return GlassUIKit.IconCheck;
            case Kind.Chevron: return GlassUIKit.IconChevron;
            default: return GlassUIKit.IconBreath;
        }
    }
}
