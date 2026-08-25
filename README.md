# ROQ Games — Game Developer Case - Bilal Kayi

Bu repo, Game Developer Case dokümanında tarif edilen 4 kısa gameplay intreactionı içermektedir.

## Gereksinimler

- **Unity 6000.3.11f1** (Unity 6.3) — URP
- Projeye resmi Unity paketleri dışında case gerekliliklerine göre 2 adet third-party paket eklenmiştir; DoTween ve QuickOutline paketleri.

## Yapı

Her case kendi klasöründe self-contained'dır; bir case'e ait scene, script,
material, prefab vb. tüm dosyalar kendi case klasöründedir.

    Assets/
      Case1_FitTheShape/   → Editor, Models, Materials, Textures, Prefabs, VFX, Scenes, SFX, Scripts (Managers)
      Case2_BlockHole/     → Models, Materials, Textures, Prefabs (Blocks/Holes/Walls/Fractured), VFX, Scenes, SFX, Scripts (Block/Hole&Floor)
      Case3_Stickerdom/    → Materials, Prefabs, Sprites (sticker + ghost), Textures, VFX, Scenes, SFX, Scripts, Shaders
      Case4_Buca/          → Materials, Textures, Prefabs (lane/hole/puck/green blocks + fractured), VFX, Scenes, SFX, Scripts

Her case klasörlerinde `Scenes/` altındaki ilgili sahnede case'e uygun olarak hazırlanmış intrectionı deneyimleyebilirsiniz.

Scripts klasöründe genelde gerekli görülmediği için alt klasör detaylandırmasına gidilmemiştir.

`Case1_FitTheShape/Scripts` klasöründe oyunda kullanılan genel PoolManager ve AudioManager dosyaları `Managers/` klasörü altına alınmıştır. Bu 2 script 4 case içinde kullanılmıştır.

## Üçüncü parti paketler

- Dotween paketi; animation, ease, squash&stretch, secondary animations, timing gibi efektler için kullanılmıştır.
- QuickOutline ise, 3D objelerde outline kullanılması gerektiği durumlarda kullanılmıştır.

## VFX / SFX

- Caselerde kullanılan VFX'leri ilgili case'in VFX klasöründe bulabilirsiniz.
- FitTheShape, BlockHole ve Stickerdom caselerinin SFX'leri case için gönderilen videolardan ses exportu yoluyla elde edilmiştir. Buca case'i için diskin fırlatılma SFX'i şu linkten alınmıştır: https://www.youtube.com/watch?v=pqEn9icjK0I

## Editor

- Caselerdeki ilgili intrectationlara kolayca ulaşılabilmesi için custom editor tool yazılmıştır. Play tuşunun solunda case sahnelerine götüren ilgili butonlar yer almaktadır.
- Ayrıca runtimeda Timescale'i editörden kontrol etmenizi sağlayan küçük bir dropdown menü de custom olarak eklenmiştir. Bu tool ilgili intrectionları hazırlarken daha hassas çalışılabilmesi amacıyla yazılmıştır. 


