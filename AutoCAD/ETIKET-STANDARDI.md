# Ruhsat Hesap AutoCAD Etiket Standardı

Bu belge, AutoCAD çizimindeki kapalı alanların Ruhsat Hesap tablolarına nasıl
bağlandığını anlatır. Dilbilgisi, Archicad eklentisinin
[zon adlandırma standardıyla](../ZON-ADLANDIRMA-STANDARDI.md) aynıdır; tek fark
AutoCAD'de kat listesi bulunmadığı için eklenen `KAT` anahtarıdır.

## 1. Hızlı başlangıç

Bir dairenin salonunu etiketlemek için:

```
RH|BLOK=A|BB=01|KAT=ZEMİN KAT|TIP=NET|ODA=3|MAHAL=SALON|NITELIK=Mesken
```

Bu metni elle yazmanız gerekmez: **`RHETIKET`** komutu polylineları seçtirir,
alanları tek tek sorar ve etiketi kendisi oluşturur. Etiket nesnenin XDATA'sına
yazılır, çizimin görünümünü değiştirmez.

Etiketledikten sonra **`RHTARA`** komutu bütün etiketli alanları okur ve proje
verisini kurar.

## 2. Kodun yapısı

```
RH | ANAHTAR=DEĞER | ANAHTAR=DEĞER | ...
```

| Parça | Görevi | Zorunluluk |
| --- | --- | --- |
| `RH` | Bu nesnenin Ruhsat Hesap tarafından okunacağını bildirir | Zorunlu ve ilk parça olmalı |
| `BLOK=A` | Alanın ait olduğu blok | Bağımsız bölüm ve kat kalemlerinde zorunlu |
| `BB=01` | Bağımsız bölüm numarası | Bağımsız bölüm alanlarında zorunlu |
| `KAT=ZEMİN KAT` | Alanın bulunduğu kat | Kat/bağımsız bölüm kalemlerinde zorunlu (yoksa kat sınırı veya `RHKAT`) |
| `TIP=NET` | Alanın hangi hesap kalemine yazılacağı | Zorunlu |
| `ODA=3` | Oda sayısı | İsteğe bağlı |
| `MAHAL=SALON` | Okunabilir mahal açıklaması | İsteğe bağlı |
| `NITELIK=Mesken` | Bağımsız bölüm niteliği | İsteğe bağlı |
| `HESAP=EMSAL` | Alanı Emsal Hesabı %30 istisna tablosuna yönlendirir | İsteğe bağlı |
| `AD=Doğu Duvarı` | İstinat duvarı gibi kalemlerin adı | İsteğe bağlı |

### Yazım kuralları

* Parçalar `|` ile ayrılır, anahtar ile değer `=` (veya `:`) ile ayrılır.
* Büyük/küçük harf ve Türkçe karakter farkı anahtarlarda önemsizdir:
  `RH|blok=A|tip=brüt` ile `RH|BLOK=A|TIP=BRUT` aynıdır.
* Boşluklar kırpılır; değerin içindeki boşluk korunur (`KAT=1. BODRUM KAT`).
* Kısa yazımlar kabul edilir: `B`=BLOK, `K`=KAT, `T`=TIP, `O`=ODA, `M`=MAHAL,
  `N`=NITELIK, `H`=HESAP, `A`=AD.
* Tanınmayan anahtarlar sessizce yok sayılır.

## 3. TIP değerleri

### Bağımsız bölüm alanları (`BB` zorunlu)

| Değer | Kabul edilen diğer yazımlar | Aktarıldığı yer |
| --- | --- | --- |
| `NET` | – | Bağımsız bölüm net alanı |
| `BRUT` | `BRÜT`, `GROSS` | Bağımsız bölüm brüt alanı |
| `EKLENTI_NET` | `EKLENTİ_NET`, `EKLENTINET` | Eklenti net alanı |
| `EKLENTI_BRUT` | `EKLENTİ_BRÜT`, `EKLENTIBRUT` | Eklenti brüt alanı |
| `BALKON` | – | Balkon alanı |

Aynı bağımsız bölümün birden çok polylineı olabilir; aynı `BLOK` + `BB`
değerine sahip bütün `NET` alanları toplanır.

### Kat ve ortak alanlar (`BB` kullanılmaz)

| Değer | Aktarıldığı yer |
| --- | --- |
| `ORTAK` | Proje genelindeki toplam ortak alan (bağımsız bölümlere dağıtılır) |
| `MERDIVEN` | Yapı İnşaat Alanı — merdiven |
| `HOL` | Yapı İnşaat Alanı — toplam kat holü |
| `ASANSOR` | Yapı İnşaat Alanı — asansör |
| `SACAK` | Yapı İnşaat Alanı — saçak |
| `SIGINAK` | Yapı İnşaat Alanı — sığınak **ve** projede ayrılan net sığınak alanı |
| `EMSAL` | Emsal Hesabı — kat emsal alanı |
| `EMSAL_DISI` | Emsal Hesabı — kat emsal dışı alanı |

`MERDIVEN`, `HOL`, `ASANSOR`, `SACAK` ve serbest TIP değerleri `HESAP=EMSAL`
eklendiğinde Yapı İnşaat Alanı yerine **Emsal Hesabı %30 istisna tablosuna**
yazılır:

```
RH|BLOK=A|KAT=1. KAT|TIP=MERDIVEN|HESAP=EMSAL
```

`SIGINAK` bunun dışındadır: her zaman Yapı İnşaat Alanı ile sığınak hesabını
besler.

### Parsel düzeyi kalemler (`BLOK` ve `KAT` kullanılmaz)

| Değer | Diğer yazımlar | Aktarıldığı yer |
| --- | --- | --- |
| `PARSEL` | `PARSEL_SINIRI` | Parsel alanı (TAKS/KAKS hesabının tabanı) |
| `OTURUM` | `TABAN`, `TAKS` | Yapı oturum alanı (TAKS kontrolü, ağaç hesabı) |
| `ISTINAT` | `ISTINAT_DUVARI` | İstinat duvarı alanı — adı `AD=` ile verilir |
| `EK_YAPI` | `EKYAPI`, `EKSTRA`, `EKSTRA_YAPI` | Foseptik, trafo, su deposu binası gibi bloğa bağlı olmayan yapılar — adı `AD=` ile verilir, Yapı İnşaat Alanı toplamına eklenir |

Bu kalemler çizimde varsa `RHPARSEL` ile elle girilen değerlerin yerine geçer;
çizimde yoksa elle girilen değerler korunur.

```
RH|TIP=EK_YAPI|AD=Foseptik
```

`EK_YAPI` istinat duvarından farklıdır: istinat duvarı ayrı raporlanır, `EK_YAPI`
ise doğrudan Yapı İnşaat Alanı'nın parçasıdır. İkisi de "Diğer Hesaplar" /
`RHTABLOLAR` çıktısında kendi tablosunda listelenir.

### Kat sınırı

| Değer | Diğer yazımlar | Görevi |
| --- | --- | --- |
| `KAT_SINIRI` | `KATSINIRI`, `KAT_CERCEVESI` | İçine düşen etiketli alanların katını belirler |

```
RH|KAT=1. KAT|TIP=KAT_SINIRI
```

Kat sınırının kendi alanı hiçbir hesaba girmez. Aynı model uzayında yan yana
duran kat planlarını birbirinden ayırmak için kullanılır; iç içe çerçevelerde
en küçüğü geçerlidir.

### Serbest TIP değerleri

Listede olmayan herhangi bir `TIP` değeri kendi alan satırını açar:

```
RH|BLOK=A|KAT=ÇATI KATI|TIP=HAVUZ_KENARI
```

* Değer önce mevcut alan satırlarıyla karşılaştırılır. Karşılaştırma büyük/küçük
  harf, boşluk, noktalama ve Türkçe karakter farklarını yok sayar: `TIP=Yangın
  Merdiveni` daha önce açılmış `yangin_merdiveni` satırını bulur.
* Eşleşme yoksa TIP metninden yeni bir satır oluşturulur.
* `HESAP=EMSAL` verilirse satır Yapı İnşaat Alanı yerine %30 tablosunda açılır.

Öngörülebilir sonuç için ASCII ve alt çizgili yazım önerilir.

## 4. Kat bilgisi nereden gelir?

Sırayla:

1. Etiketteki `KAT=` değeri,
2. Nesnenin içinde kaldığı `TIP=KAT_SINIRI` çerçevesinin `KAT=` değeri,
3. `RHKAT` komutuyla belirlenen aktif kat.

Üçü de yoksa alan okunmaz ve `RHTARA` komut satırında hangi nesnenin eksik
olduğunu yazar.

Kat adları isimlerinden sıralanır:

```
2. BODRUM < 1. BODRUM < ZEMİN KAT < ASMA KAT < 1. KAT < 2. KAT < ÇATI KATI
```

Tanınmayan adlar tabloların sonunda, ilk görüldükleri sırayla listelenir.

`KAT=` değeri büyük/küçük harf ve boşluk farkına bakılmadan karşılaştırılır:
`KAT=1.KAT`, `KAT=1. Kat` ve `KAT=1.  KAT` aynı kat sayılır ve tek satırda
birleştirilir. Aynı katın etiketleri arasında yine de tek bir yazım kullanmanız
önerilir; farklı yazımlar birleştiğinde `RHTARA` bunu komut satırında bildirir
("Aynı kat farklı yazılmış ve birleştirildi: ..."), böylece hangi iki yazımı
tekleştirmeniz gerektiğini görürsünüz.

### Dubleks bağımsız bölümler

Bir bağımsız bölümün alanları birden çok kata yayılabilir. Bu durumda:

* Bütün alanlar aynı bağımsız bölümde toplanır,
* Bağımsız bölümün "bulunduğu kat" değeri, `NET`/`BRUT` etiketli alanların en
  alt katıdır.

## 5. Etiket üç kaynaktan okunabilir

| Öncelik | Kaynak | Ne zaman kullanılır |
| --- | --- | --- |
| 1 | **XDATA** (`RUHSATHESAP`) | `RHETIKET` komutunun yazdığı yer — önerilen yöntem |
| 2 | **Nesne içindeki yazı** | Polyline içine düşen, `RH\|` ile başlayan TEXT/MTEXT |
| 3 | **Katman adı** | `RH-BLOK_A-BB_01-TIP_NET` |

Katman adı yazımında AutoCAD `|` ve `=` karakterlerine izin vermediği için
parçalar `-`, anahtar/değer ise **ilk** `_` ile ayrılır. Böylece
`TIP_EKLENTI_NET` doğru biçimde `TIP = EKLENTI_NET` olarak okunur.

## 6. Hangi nesneler ölçülebilir?

`LWPOLYLINE`, `POLYLINE`, `CIRCLE`, `ELLIPSE`, `SPLINE`, `REGION` ve `HATCH`.

* Polyline kapalı değilse alan, uçlar birleştirilmiş varsayılarak hesaplanır ve
  komut satırında uyarı verilir.
* Alan ölçüsü çizim biriminden m²'ye `RHBIRIM` ayarıyla çevrilir.
* Blok referansı (`INSERT`) içindeki nesneler taranmaz.

## 7. Uygulama örneği — bir dairenin etiketleri

| Polyline | Etiket |
| --- | --- |
| Salon | `RH\|BLOK=A\|BB=01\|KAT=1. KAT\|TIP=NET\|ODA=3\|MAHAL=SALON\|NITELIK=Mesken` |
| Yatak odası | `RH\|BLOK=A\|BB=01\|KAT=1. KAT\|TIP=NET\|MAHAL=YATAK ODASI` |
| Mutfak | `RH\|BLOK=A\|BB=01\|KAT=1. KAT\|TIP=NET\|MAHAL=MUTFAK` |
| Daire dış sınırı | `RH\|BLOK=A\|BB=01\|KAT=1. KAT\|TIP=BRUT` |
| Balkon | `RH\|BLOK=A\|BB=01\|KAT=1. KAT\|TIP=BALKON` |
| Kat merdiveni | `RH\|BLOK=A\|KAT=1. KAT\|TIP=MERDIVEN` |
| Kat emsal sınırı | `RH\|BLOK=A\|KAT=1. KAT\|TIP=EMSAL` |
| Parsel sınırı | `RH\|TIP=PARSEL` |

`RHTARA` sonrası panel/tablo karşılığı:

* `01` numaralı bağımsız bölüm: net alan = salon + yatak odası + mutfak,
  brüt alan = dış sınır, balkon alanı = balkon, oda sayısı = 3,
  nitelik = Mesken, bulunduğu kat = 1. KAT.
* 1. KAT'ın Yapı İnşaat Alanı satırında merdiven alanı.
* Emsal Hesabı tablosunda 1. KAT emsal alanı.
* Parsel alanı ve buna bağlı TAKS/KAKS kontrolleri.

## 8. Sık yapılan hatalar

| Belirti | Nedeni |
| --- | --- |
| `alan sıfır (polyline kapalı mı?)` | Nesne alan oluşturmuyor; polyline'ı kapatın veya `HATCH` kullanın |
| `BB numarası eksik` | `NET`/`BRUT`/`BALKON` gibi bağımsız bölüm tiplerinde `BB=` zorunludur |
| `kat belirsiz` | `KAT=` yok, kat sınırı yok ve `RHKAT` ile aktif kat belirlenmemiş |
| `BLOK eksik` | Kat ve bağımsız bölüm kalemlerinde `BLOK=` zorunludur |
| Alanlar 10.000 kat büyük | Çizim santimetre; `RHBIRIM` ile birimi düzeltin |
| Aynı alan iki kez sayıldı | Hem daire brütü hem de mahaller `TIP=BRUT` etiketlenmiş; mahaller `TIP=NET` olmalı |
| Bir kattaki BRÜT/EMSAL tabloya işlemiyor | O kattaki etiketlerden biri `KAT=` değerini farklı yazmış (`1.KAT` / `1. Kat`); `RHTARA` çıktısındaki "Aynı kat farklı yazılmış" uyarısını kontrol edin, tüm etiketlerde aynı yazımı kullanın |
| Foseptik/trafo gibi yapılar inşaat alanına eklenmiyor | `TIP=EK_YAPI` yerine tanınmayan başka bir TIP veya `BLOK=`/`KAT=` ile etiketlenmiş; `EK_YAPI` blok/kat gerektirmez, `AD=` ile adlandırılır |
