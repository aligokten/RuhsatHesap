# Ruhsat Hesap — AutoCAD Eklentisi

Bu klasör, Ruhsat Hesap programının **AutoCAD sürümüdür**. Archicad eklentisi
zonlardan alan okurken, AutoCAD sürümü **kapalı polylineların alanını** okur:
polylineları RH etiketiyle işaretlersiniz, eklenti alanları ölçer, bağımsız
bölüm / kat / blok bazında toplar ve emsal, TAKS/KAKS, yapı inşaat alanı, kat
irtifakı ve otopark tablolarını üretir.

Üretilen tablolar üç biçimde çıkar:

| Çıktı | Nasıl |
| --- | --- |
| AutoCAD TABLE nesnesi | `RHALANTABLO`, `RHEMSAL`, `RHTABLOLAR` … doğrudan çizime |
| Excel çalışma kitabı (`.xlsx`) | `RHEXCEL` |
| CSV / JSON | `RHCSV`, `RHJSONKAYDET` |

Proje verisi **DWG'nin içinde** saklanır (Named Object Dictionary); Archicad
eklentisinin PLN içine yazdığı Add-On Object'in AutoCAD karşılığıdır. JSON
biçimi ortaktır: aynı dosya web panelinde, Archicad eklentisinde ve AutoCAD'de
açılabilir.

## Kurulum

### Hazır paket (önerilen)

1. [Releases](https://github.com/aligokten/RuhsatHesap/releases) sayfasından
   `RuhsatHesap-AutoCAD-<sürüm>.zip` dosyasını indirin.
2. Zip'in içindeki `RuhsatHesap.bundle` klasörünü şuraya kopyalayın:

   ```
   %APPDATA%\Autodesk\ApplicationPlugins\
   ```

3. Kopyaladığınız klasördeki `.dll` dosyalarının **engelini kaldırın**.
   İnternetten inen dosyaları Windows işaretler ve AutoCAD işaretli bir
   assembly'i sessizce yüklemez.
4. AutoCAD'i yeniden başlatın. Komut satırında `RHYARDIM` yazın.

PowerShell ile tek seferde:

```powershell
$zip  = "$env:USERPROFILE\Downloads\RuhsatHesap-AutoCAD-0.6.0.zip"
$dest = "$env:APPDATA\Autodesk\ApplicationPlugins"
Expand-Archive $zip -DestinationPath $dest -Force
Get-ChildItem "$dest\RuhsatHesap.bundle" -Recurse -Filter *.dll | Unblock-File
```

Her `autocad-v*` etiketi **Release AutoCAD Plugin** iş akışını çalıştırıp yeni
bir Release yayımlar. Ara derlemeler için GitHub Actions → **Build AutoCAD
Plugin** çalışmasının `RuhsatHesap-AutoCAD-bundle` artifact'ı da kullanılabilir
(90 gün saklanır).

Paket hem `.NET Framework 4.8` (AutoCAD 2019–2024) hem de `.NET 8`
(AutoCAD 2025 ve sonrası) sürümlerini içerir; AutoCAD kendi sürümüne uyanı
yükler.

### NETLOAD ile

Tek seferlik denemek için `RuhsatHesap.Acad.dll` dosyasını `NETLOAD` komutuyla
yükleyin. `RuhsatHesap.Core.dll` aynı klasörde bulunmalıdır.

### Kaynaktan derleme

```bat
cd AutoCAD
dotnet test  tests\RuhsatHesap.Core.Tests\RuhsatHesap.Core.Tests.csproj -c Release
dotnet build src\RuhsatHesap.Acad\RuhsatHesap.Acad.csproj -c Release
```

AutoCAD referansları [`AutoCAD.NET`](https://www.nuget.org/packages/AutoCAD.NET)
NuGet paketinden gelir; makinede AutoCAD kurulu olması gerekmez. Çıktı:

```
AutoCAD\src\RuhsatHesap.Acad\bin\Release\net48\RuhsatHesap.Acad.dll
AutoCAD\src\RuhsatHesap.Acad\bin\Release\net8.0-windows\RuhsatHesap.Acad.dll
```

## 5 adımda ilk hesap

```
1) RHBIRIM      → çizim birimini seçin (cm / mm / m). Alanlar buna göre m²'ye çevrilir.
2) RHKAT        → üzerinde çalıştığınız katı yazın (örn. ZEMİN KAT).
3) RHETIKET     → daire polylinelarını seçin, TIP=NET, BLOK=A, BB=01 girin.
4) RHTARA       → çizimi tarar, bütün etiketli alanları proje verisine işler.
5) RHTABLOLAR   → bütün ruhsat tablolarını çizime yerleştirir. (RHEXCEL → .xlsx)
```

Parsel bilgileri ve TAKS/KAKS oranları için `RHPARSEL`; emsal kontrolü bu
oranlara göre hesaplanır.

## Komutlar

### Form penceresi

| Komut | İşlevi |
| --- | --- |
| `RHPANEL` | **Ruhsat Hesap formunu** açar/kapatır — sabitlenebilir palet |
| `RHPANELYENILE` | Formu çizimdeki güncel veriyle yeniler |

Form üç sekmelidir:

* **Parsel** — proje adı, il/ilçe/mahalle, ada/parsel, parsel alanı, TAKS, emsal
  yöntemi (KAKS veya doğrudan), yapı oturum alanı, ortak alan ve otopark.
  Parsel alanı ile oturum alanının yanındaki **Çizimden ölç** düğmesi
  polyline seçtirip m² değerini kutuya yazar.
* **Bağımsız Bölümler** — blok, BB no, kat, nitelik, oda sayısı, brüt/net/eklenti
  ve balkon alanları, arsa payı ve malik bilgisi; satır ekleyip silebilirsiniz.
* **Katlar / Emsal** — blok ve kat bazında emsal alanı ile emsal dışı alan.
  %30 istisna ve yapı inşaat kalemleri etiketlerden okunduğu için burada
  yalnız toplamları görünür.

Üstteki düğmeler: **Yenile**, **Kaydet** (formu çizime yazar), **Çizimi Tara**
(`RHTARA`), **Tabloları Çiz** (`RHTABLOLAR`), **Excel**, **JSON Kaydet**.
Alt satırda hesaplanan emsal, emsal bakiyesi/aşımı, bağımsız bölüm sayısı, yapı
inşaat alanı ve otopark durumu canlı olarak görünür.

Elle girilen değerlerle etiketten gelenler yan yana yaşar: `RHTARA` her
çalıştığında yalnız kendi yazdığı kalemleri yeniden kurar, formda yazdıklarınıza
dokunmaz.

### Ayarlar

| Komut | İşlevi |
| --- | --- |
| `RHYARDIM` | Komut listesini yazar |
| `RHAYAR` | Çizim birimi, aktif blok/kat, tablo yazı yüksekliği ve yazı tipi, etiket yazısı |
| `RHBIRIM` | Yalnızca çizim birimi (metre / santimetre / milimetre) |
| `RHKAT` | `KAT=` yazılmayan etiketlerin varsayılan katı |
| `RHPARSEL` | Proje adı, il/ilçe, ada/parsel, parsel alanı, TAKS, KAKS/emsal, oturum alanı |
| `RHOTOPARK` | Projede ayrılan otopark sayısı |
| `RHVERI` | Çizimdeki proje verisinin özeti |
| `RHTEMIZLE` | Çizimdeki proje verisini siler (etiketler kalır) |

`RHPARSEL`, parsel alanını ve yapı oturum alanını isterseniz doğrudan
çizimden ölçer.

### Etiketleme

| Komut | İşlevi |
| --- | --- |
| `RHETIKET` | Seçilen kapalı nesnelere RH etiketi yazar (XDATA) |
| `RHETIKETSIL` | Seçilen nesnelerin etiketini siler |
| `RHSOR` | Bir nesnenin etiketini, tipini ve m² alanını yazar |
| `RHTARA` | Model uzayını tarar, proje verisini yeniden kurar |

`RHTARA` tekrar tekrar çalıştırılabilir: her seferinde yalnız kendi yazdığı
değerleri yeniler, elle girdiğiniz kalemlere dokunmaz.

### Tablolar

| Komut | Tablo |
| --- | --- |
| `RHALANTABLO` | Seçilen polylineların alan hesap tablosu (etiketsizler dâhil) |
| `RHALANOZET` | Aynı seçimin blok / kat / tip bazında özeti |
| `RHEMSAL` | Emsal hesap tablosu + %30 istisna sütunları + emsal kontrolü |
| `RHBB` | Bağımsız bölüm alan tablosu (net, brüt, eklenti, balkon, otopark payı) |
| `RHINSAAT` | Yapı inşaat alanı (kat bazlı kalemler) |
| `RHINSAATALANI` | İnşaat alanı: satırlar kat, sütunlar blok |
| `RHIRTIFAK` | Kat irtifakı tablosu |
| `RHOZET` | Parsel, TAKS/KAKS kontrolleri, ağaç, otopark özeti |
| `RHTABLOLAR` | Yukarıdaki tabloların tamamı, alt alta |

Tablolar `RH-TABLO` katmanına, seçtiğiniz noktadan başlayarak yerleştirilir ve
normal AutoCAD tablosu oldukları için tablo stiliyle biçimlendirilebilir.
Bütün hücreler ortalanır, arka plan dolgusu uygulanmaz; renk ve çizgi denetimi
tamamen çizimdeki tablo stilindedir. Yazı tipi varsayılan olarak **ISOCPEUR** stilidir —
çizimde yoksa eklenti bu adla bir yazı stili oluşturur. `RHAYAR` ile başka bir
stil adı verebilirsiniz (boş bırakırsanız tablo stilinin kendi yazı stili
kullanılır).

### Aktarım

| Komut | İşlevi |
| --- | --- |
| `RHEXCEL` | Yedi sayfalı `.xlsx` çalışma kitabı |
| `RHCSV` | Tabloların tamamı tek `.csv` dosyasında |
| `RHJSONKAYDET` | Proje verisi (`.json`) — web paneli ve Archicad ile ortak biçim |
| `RHJSONAC` | Web panelinden veya Archicad'den gelen `.json` dosyasını okur |

## Etiket biçimi

```
RH|BLOK=A|BB=01|KAT=ZEMİN KAT|TIP=NET|ODA=3|MAHAL=SALON|NITELIK=Mesken
```

Ayrıntılı anlatım: [ETIKET-STANDARDI.md](ETIKET-STANDARDI.md).

Etiket üç yerden okunabilir; sırayla denenir:

1. **XDATA** — `RHETIKET` komutunun yazdığı yer. Görünmez, kopyalamada korunur.
2. **İçindeki yazı** — polyline içine düşen, `RH|` ile başlayan TEXT/MTEXT.
3. **Katman adı** — `RH-BLOK_A-BB_01-TIP_NET` (AutoCAD katman adında `|` ve `=`
   kullanılamadığı için `-` ve `_` ile).

## Kat bilgisi

DWG'de Archicad'deki gibi bir kat listesi yoktur. Kat şu sırayla belirlenir:

1. Etiketteki `KAT=` değeri,
2. Nesnenin içinde kaldığı **kat sınırı** çerçevesi
   (`RH|KAT=1. KAT|TIP=KAT_SINIRI` etiketli polyline) — aynı model uzayında
   yan yana duran kat planları için,
3. `RHKAT` ile belirlenen aktif kat.

Kat adları isimlerinden sıralanır: `2. BODRUM` < `1. BODRUM` < `ZEMİN KAT` <
`ASMA KAT` < `1. KAT` < `2. KAT` < `ÇATI KATI`.

## Çizim birimi

Alanlar m² cinsinden hesaplanır. Çizim santimetre ise `RHBIRIM` ile
santimetreyi seçmeniz **şarttır**; aksi hâlde alanlar 10.000 kat büyük çıkar.
İlk açılışta değer `INSUNITS` sistem değişkeninden tahmin edilir, tahmin
edilemezse santimetre kabul edilir.

## Veri nerede saklanır?

* **Etiketler** → nesnenin XDATA'sında (`RUHSATHESAP` uygulama adı).
* **Proje verisi ve ayarlar** → çizimin Named Object Dictionary'sinde,
  `RUHSATHESAP` sözlüğü altında.

İkisi de DWG'nin parçasıdır; kalıcı olması için çizimi normal `KAYDET`
komutuyla kaydetmeniz gerekir.

## Archicad eklentisiyle ilişkisi

| | Archicad eklentisi | AutoCAD eklentisi |
| --- | --- | --- |
| Alan kaynağı | Zon nesneleri | Kapalı polyline / hatch / region |
| Etiket yeri | Zon Adı | XDATA (yedek: yazı, katman adı) |
| Kat | Archicad ana katı | `KAT=`, kat sınırı veya aktif kat |
| Veri saklama | PLN içindeki Add-On Object | DWG Named Object Dictionary |
| Arayüz | Gömülü web paleti | AutoCAD komutları + çizime tablo |
| Ortak nokta | `ruhsat-hesap-archicad` JSON biçimi ve aynı hesap motoru | |

Hesap sonuçları bilinçli olarak aynıdır: emsal, TAKS/KAKS, ağaç ve otopark
formülleri `Src/CalculationEngine.cpp` dosyasından birebir taşınmıştır.

## Proje yapısı

```
AutoCAD/
  src/RuhsatHesap.Core/      AutoCAD'den bağımsız çekirdek (netstandard2.0)
    Json/                    Bağımlılıksız JSON okuyucu/yazıcı
    Model/                   Proje verisi ve dosya biçimi
    Tagging/                 RH etiket dilbilgisi, kat sıralaması, senkronizasyon
    Reporting/               Tablo modeli, tablo üreticileri, XLSX ve CSV yazıcıları
    CalculationEngine.cs     Emsal / TAKS / ağaç / otopark hesapları
  src/RuhsatHesap.Acad/      AutoCAD katmanı (net48 + net8.0-windows)
    Commands/                RH* komutları
    DrawingScanner.cs        Çizimden alan okuma
    TableRenderer.cs         ReportTable → AutoCAD TABLE
  tests/                     Çekirdek testleri (xUnit)
  bundle/PackageContents.xml ApplicationPlugins tanımı
```

Çekirdekte hiçbir NuGet bağımlılığı yoktur; eklenti AutoCAD'in kendi
AppDomain'ine yüklendiği için üçüncü parti bir assembly sürümünün AutoCAD'in
yüklediğiyle çakışması istenmez.

## Bilinen sınırlar

* Blok referansı (`INSERT`) içindeki polylineler taranmaz; alan nesneleri model
  uzayında olmalıdır.
* Etiketli nesne kapalı değilse alan, kapatılmış varsayılarak hesaplanır ve
  komut satırında uyarı verilir. Hiç ölçülemeyen etiketli nesneler `RHTARA`
  çıktısında tek tek listelenir; hesaba girmedikleri için tabloda sıfır görürsünüz.
* `RHTARA` her taramanın sonunda TIP bazında kaç adet ve kaç m² okunduğunu
  yazar — bir tablo boş çıktığında önce buraya bakın.
* `RHJSONAC` web panelinin zarf biçimini okur, ancak dosyayı yeniden yazarken
  ortak (`ruhsat-hesap-archicad`) biçimde kaydeder.
