# Ruhsat Hesap Archicad Zon Kodlari Kullanim Kilavuzu

**Uygulama:** Ruhsat Hesap  
**Uyumluluk:** Archicad 29 / Ruhsat Hesap 0.5.9 zon okuyucu kurallari  
**Kapsam:** Zonlardan blok, bagimsiz bolum, kat, net/brut/eklenti/balkon, ortak alan, yapi insaat alanlari (merdiven, hol, siginak, sacak, asansor ve serbest ozel kalemler), Emsal Hesabi %30 tablosuna `HESAP=EMSAL` ile yonlendirme, emsal alanlari, siginak alani, oda sayisi ve nitelik aktarimi

> Bu dokuman zon adlarinin Ruhsat Hesap tarafindan nasil okunacagini aciklar. Zon kodu olmayan normal Archicad zonlari degistirilmez ve hesaba alinmaz.

## 1. Hizli baslangic

Archicad'de bir zonun **Zon Adi / Zone Name** alanina asagidaki bicimde kod yazilir:

```text
RH|BLOK=A|BB=01|TIP=NET|ODA=3|MAHAL=SALON|NITELIK=MESKEN
```

Zonun **Zon Numarasi / Zone Number** alani ayrica doldurulabilir. `BB` kodu Zon Adinda yoksa eklenti, Zon Numarasini bagimsiz bolum numarasi olarak kullanir.

Aktarim icin Ruhsat Hesap paletinde:

1. `Zonlari Aktar` veya web panelindeki `Zonlardan Guncelle` dugmesine basin.
2. Durum satirindaki taranan ve aktarilan zon sayilarini kontrol edin.
3. `PLN Kaydet` ile hesap verisini proje dosyasina yazin.

## 2. Kodun yapisi

Kod, dikey cizgi `|` ile ayrilan parcalardan olusur:

```text
RH | BLOK=A | BB=01 | TIP=NET | ODA=3 | MAHAL=SALON | NITELIK=MESKEN
```

| Parca | Gorevi | Zorunluluk |
|---|---|---|
| `RH` | Bu zonun Ruhsat Hesap tarafindan okunacagini bildirir | Zorunlu ve ilk parca olmali |
| `BLOK=A` | Zonun ait oldugu blok | Zorunlu |
| `BB=01` | Bagimsiz bolum numarasi | Bagimsiz bolum TIP kodlarinda Zon Numarasi bos ise zorunlu; ortak/kat TIP kodlarinda kullanilmaz |
| `TIP=NET` | Alanin hangi hesap kalemine yazilacagi | Zorunlu |
| `ODA=3` | Konut oda sinifi / oda sayisi | Istege bagli, varsayilan 0 |
| `MAHAL=SALON` | Zonun okunabilir mahal aciklamasi | Istege bagli, hesapta kullanilmaz |
| `NITELIK=MESKEN` | Bagimsiz bolum niteligi | Istege bagli |

### Temel yazim kurallari

- Ilk parca tam olarak `RH` anlamina gelmelidir. `X|RH|...` seklindeki bir ad okunmaz.
- Anahtar ile deger arasinda `=` kullanilmasi onerilir. `:` da kabul edilir.
- Parca sirasi `RH` sonrasinda degisebilir; ancak ofis standardi icin tabloda verilen sira korunmalidir.
- Anahtarlar buyuk-kucuk harfe duyarsizdir. `blok=a`, `Blok=A` ve `BLOK=A` okunur.
- Turkce karakterler anahtar ve sinif degerlerinde taninir. Yine de ekipler arasi uyum icin kodlarda ASCII yazim onerilir: `BRUT`, `EKLENTI_NET` gibi.
- Parcalarin basindaki ve sonundaki bosluklar temizlenir. Buna ragmen bosluksuz kod kullanmak hata riskini azaltir.
- Tanimlanmamis anahtarlar atlanir. Ayni anahtar birden fazla yazilirsa son degerin kullanilmasi mumkundur; tekrarli anahtar kullanmayin.

## 3. Tanimlanan anahtarlar

| Uzun anahtar | Kisa ad | Ornek | Paneldeki karsiligi | Aciklama |
|---|---|---|---|---|
| `BLOK` | `B` | `BLOK=A` | Blok | Blok adi normalize edilerek buyuk harfe cevrilir |
| `BB` veya `BAGIMSIZBOLUM` | `BB` | `BB=01` | Bagimsiz Bolum No | Metin olarak saklanir; `01` ile `1` farkli kayittir |
| `TIP` | `T` | `TIP=NET` | Alan sutunu | Zon alaninin hangi hesap modulune ve kaleme yazilacagini belirler |
| `ODA` | `O` | `ODA=3` | Oda sayisi | Negatif olmayan tam sayi olmalidir |
| `MAHAL` | `M` | `MAHAL=SALON` | - | Okunur ancak mevcut surumde tabloya kaydedilmez |
| `NITELIK` | `N` | `NITELIK=MESKEN` | Nitelik | Kat irtifaki ve bagimsiz bolum verisinde kullanilir |

### Uzun ve kisa yazim

Uzun yazim, proje teslimi ve ekip calismasi icin onerilen standarttir:

```text
RH|BLOK=A|BB=01|TIP=NET|ODA=3|MAHAL=SALON|NITELIK=MESKEN
```

Kisa yazim da ayni sonucu verir:

```text
RH|B=A|BB=01|T=NET|O=3|M=SALON|N=MESKEN
```

## 4. Alan tipi - TIP degerleri

| Onerilen deger | Kabul edilen diger yazimlar | Aktarilan alan |
|---|---|---|
| `NET` | - | Bagimsiz bolum net alani |
| `BRUT` | `BRÜT`, `GROSS` | Bagimsiz bolum brut alani |
| `EKLENTI_NET` | `EKLENTİ_NET`, `EKLENTINET` | Bagimsiz bolum eklenti net alani |
| `EKLENTI_BRUT` | `EKLENTİ_BRÜT`, `EKLENTIBRUT` | Bagimsiz bolum eklenti brut alani |
| `BALKON` | - | Balkon alani |
| `ORTAK` | - | Bagimsiz bolum genel brut hesabinda dagitilan toplam ortak alan |
| `MERDIVEN` | - | Yapi Insaat Alani tablosunda kat bazli merdiven alani |
| `HOL` | - | Yapi Insaat Alani tablosunda kat bazli toplam hol alani |
| `EMSAL` | - | Emsal Hesabi tablosunda kat bazli emsal alani |
| `EMSAL_DISI` | `EMSALDISI` | Emsal Hesabi tablosunda kat bazli emsal disi alan |
| `SIGINAK` | `SIĞINAK` | Yapi Insaat Alani siginak satiri ve projede ayrilan net siginak alani |
| `SACAK` | `SAÇAK` | Yapi Insaat Alani (varsayilan) veya `HESAP=EMSAL` ile Emsal Hesabi %30 tablosunda kat bazli sacak alani |
| `ASANSOR` | `ASANSÖR` | Yapi Insaat Alani (varsayilan) veya `HESAP=EMSAL` ile Emsal Hesabi %30 tablosunda kat bazli asansor alani |

### Alanin Archicad'den alinma bicimi

Eklenti her taninan zonun Archicad tarafindan hesaplanan **zon net alanini** okur. `TIP` degeri bu sayinin panelde hangi alana yazilacagini belirler.

Bu nedenle:

- `TIP=NET` zonu, net mahal sinirini temsil etmelidir.
- `TIP=BRUT` zonu, bagimsiz bolumun brut sinirini temsil eden ayri bir zon poligonu olmalidir.
- `TIP=BALKON` zonu, balkon poligonunu temsil etmelidir.
- `TIP=EKLENTI_NET` ve `TIP=EKLENTI_BRUT` zonlari, eklenti sinirlarina gore ayri cizilmelidir.

`TIP=BRUT` yazmak Archicad zon alanini kendiliginden duvar disina buyutmez. Brut sinirin modelde dogru kurulmasi kullanicinin sorumlulugundadir.

## 5. Hangi bilgi nereden gelir?

| Ruhsat Hesap verisi | Kaynak |
|---|---|
| Blok | Zon Adindaki `BLOK` |
| Bagimsiz bolum no | Once Zon Adindaki `BB`; yoksa Archicad Zon Numarasi |
| Kat | Zonun Archicad ana kati / home story bilgisi |
| Net alan | `TIP=NET` zonlarinin alan toplami |
| Brut alan | `TIP=BRUT` zonlarinin alan toplami |
| Eklenti net | `TIP=EKLENTI_NET` zonlarinin alan toplami |
| Eklenti brut | `TIP=EKLENTI_BRUT` zonlarinin alan toplami |
| Balkon | `TIP=BALKON` zonlarinin alan toplami |
| Oda sayisi | Ayni bagimsiz bolumdeki `ODA` degerlerinin en buyugu |
| Nitelik | Bos olmayan `NITELIK` degeri |
| Mahal adi | `MAHAL` okunur; mevcut surumde hesap kaydina aktarilmaz |
| Toplam ortak alan | `TIP=ORTAK` zonlarinin proje genelindeki toplami |
| Merdiven / hol / siginak | Ilgili TIP zonlarinin blok ve ana kat bazindaki toplami |
| Emsal / emsal disi | Ilgili TIP zonlarinin blok ve ana kat bazindaki toplami |

## 6. Bagimsiz bolumlerin birlestirilme kurali

Eklenti her zonu su ikiliye gore gruplar:

```text
BLOK + BB
```

Ornegin `BLOK=A` ve `BB=01` yazan tum taninan zonlar tek bir bagimsiz bolum kaydinda birlesir.

- Ayni `TIP` degerine sahip zonlarin alanlari toplanir.
- `ODA` icin toplam degil, en buyuk deger kullanilir.
- `NITELIK` icin tutarli bir deger kullanilmalidir. En guvenli yontem, niteligi ana `NET` zonlarinda ayni yazmak veya yalnizca bir ana zonda belirtmektir.
- `BLOK=A|BB=01` ile `BLOK=A|BB=1` iki farkli bagimsiz bolumdur.
- `BLOK=A` ile `BLOK=A BLOK` iki farkli bloktur. Bloklarda sadece `A`, `B`, `C` bicimi onerilir.

## 7. Kat bilgisinin belirlenmesi

Kat adi zon koduna yazilmaz. Eklenti, zonun Archicad'deki ana katini otomatik okur.

Ayni bagimsiz bolume bagli zonlar farkli katlardaysa kat secim onceligi soyledir:

1. `NET` veya `BRUT` zonu
2. `BALKON` zonu
3. `EKLENTI_NET` veya `EKLENTI_BRUT` zonu

Ayni oncelikte birden fazla kat varsa kat indeksi daha dusuk olan kat secilir. Bu kural, bodrumdaki eklentinin zemin kattaki dairenin katini degistirmesini onler.

### Dubleks ve cok katli bagimsiz bolumler

Dubleks bir dairenin tum zonlarinda ayni `BLOK` ve `BB` kullanilir. Net alanlar toplanir. Panelde bagimsiz bolum icin tek bir kat alani bulundugundan, ana kat yukaridaki oncelik kuraliyla secilir.

## 8. Oda sayisi ve nitelik

### ODA

`ODA`, siginak hesabinda kullanilan konut oda sinifini bildiren negatif olmayan tam sayidir:

```text
ODA=1
ODA=2
ODA=3
```

`ODA=3+1`, `ODA=UC` veya ondalikli deger kullanmayin. Degeri proje ve ilgili yonetmelik yorumunuza gore tam sayi olarak girin.

Ayni bagimsiz bolumdeki her zon icin `ODA` yazmak zorunlu degildir. Ancak yazilan degerler farkliysa en buyuk deger alinacagi icin ekip standardinda ayni BB'ye ait zonlarda tutarli deger kullanin.

### NITELIK

Onerilen nitelik degerleri:

```text
NITELIK=MESKEN
NITELIK=OFIS
NITELIK=DUKKAN
NITELIK=DEPO
```

Nitelik serbest metindir. Yazim birligi icin proje genelinde tek bir sozluk kullanin.

## 9. Uygulama ornegi - bir dairenin tum zonlari

A Blok, 01 numarali, 3 odali bir mesken icin ornek zonlar:

```text
RH|BLOK=A|BB=01|TIP=NET|ODA=3|MAHAL=SALON|NITELIK=MESKEN
RH|BLOK=A|BB=01|TIP=NET|ODA=3|MAHAL=MUTFAK|NITELIK=MESKEN
RH|BLOK=A|BB=01|TIP=NET|ODA=3|MAHAL=YATAK_ODASI_1|NITELIK=MESKEN
RH|BLOK=A|BB=01|TIP=NET|ODA=3|MAHAL=YATAK_ODASI_2|NITELIK=MESKEN
RH|BLOK=A|BB=01|TIP=NET|ODA=3|MAHAL=HOL|NITELIK=MESKEN
RH|BLOK=A|BB=01|TIP=NET|ODA=3|MAHAL=BANYO|NITELIK=MESKEN
RH|BLOK=A|BB=01|TIP=BRUT|ODA=3|MAHAL=DAIRE_BRUT|NITELIK=MESKEN
RH|BLOK=A|BB=01|TIP=BALKON|ODA=3|MAHAL=BALKON|NITELIK=MESKEN
RH|BLOK=A|BB=01|TIP=EKLENTI_NET|ODA=3|MAHAL=DEPO|NITELIK=MESKEN
RH|BLOK=A|BB=01|TIP=EKLENTI_BRUT|ODA=3|MAHAL=DEPO_BRUT|NITELIK=MESKEN
```

Ornek alanlar:

| Zon grubu | Alanlar | Panel sonucu |
|---|---|---|
| NET | 24,50 + 12,25 + 14,00 + 12,50 + 6,75 + 5,00 | 75,00 m2 net |
| BRUT | 92,40 | 92,40 m2 brut |
| BALKON | 8,20 | 8,20 m2 balkon |
| EKLENTI_NET | 4,00 | 4,00 m2 eklenti net |
| EKLENTI_BRUT | 5,20 | 5,20 m2 eklenti brut |

## 10. Zon Numarasini BB olarak kullanma

Bagimsiz bolum numarasini Archicad'in Zon Numarasi alaninda tutuyorsaniz, Zon Adindan `BB` parcasini kaldirabilirsiniz:

```text
Zon Adi:      RH|BLOK=A|TIP=NET|ODA=3|MAHAL=SALON|NITELIK=MESKEN
Zon Numarasi: 01
```

Zon Adinda `BB` varsa o deger onceliklidir. Hem Zon Adini hem Zon Numarasini kullanacaksaniz ikisinin ayni oldugunu kontrol edin.

## 11. Cok bloklu proje ornekleri

```text
RH|BLOK=A|BB=01|TIP=NET|ODA=2|MAHAL=DAIRE|NITELIK=MESKEN
RH|BLOK=A|BB=01|TIP=BRUT|ODA=2|MAHAL=DAIRE_BRUT|NITELIK=MESKEN

RH|BLOK=B|BB=01|TIP=NET|ODA=3|MAHAL=DAIRE|NITELIK=MESKEN
RH|BLOK=B|BB=01|TIP=BRUT|ODA=3|MAHAL=DAIRE_BRUT|NITELIK=MESKEN
```

A ve B bloklarinda ayni BB numarasi kullanilabilir; blok adi kayitlari birbirinden ayirir.

## 12. Archicad'de adim adim uygulama

1. Archicad Zon araciyla hesaplanacak mahal veya siniri olusturun.
2. Zonun ana katinin dogru oldugunu kontrol edin.
3. Zon Ayarlarinda **Zon Adi** alanina `RH|...` kodunu yazin.
4. `BB` kullanmiyorsaniz **Zon Numarasi** alanina bagimsiz bolum numarasini yazin.
5. Zon alaninin sifirdan buyuk ve sinirlarinin guncel oldugunu kontrol edin.
6. Net mahaller, brut sinir, balkon ve eklentiler icin gereken ayri zonlari olusturun.
7. Ruhsat Hesap paletini acin ve `Zonlari Aktar` dugmesine basin.
8. Web panelinde kodun hedefine gore **Bagimsiz Bolumler**, **Yapi Insaat Alani**, **Emsal Hesabi** veya **Siginak Hesabi** modulunu acin.
9. Blok, kat ve ilgili alan sonuclarini kontrol edin.
10. Sonuclar dogruysa `PLN Kaydet` ile veriyi proje icinde saklayin.

## 13. Yeniden aktarim ve veri guvenligi

- Ayni zonlari tekrar aktarmak alanlari iki kez eklemez. Eklenti once zon baglantili otomatik degerleri temizler, sonra guncel modeli yeniden hesaplar.
- Manuel olusturulmus ve hicbir zona baglanmamis bagimsiz bolumler korunur.
- Daha once aktarilan bir zon silinirse veya kodu gecersiz hale gelirse, sonraki aktarimda ilgili otomatik alanlar yeniden hesaplanir. Kayit kalabilir ancak zona bagli alanlari sifirlanabilir.
- Ayni fiziksel alani temsil eden iki ayri RH zonu ayni `BLOK`, `BB` ve `TIP` ile birakilirsa alan iki kez toplanir. Cakisan veya kopya zonlari kontrol edin.

## 14. Gecersiz veya desteklenmeyen kodlar

| Durum | Sonuc | Cozum |
|---|---|---|
| Ad `RH` ile baslamiyor | Zon tamamen yok sayilir | Ilk parcayi `RH` yapin |
| `BLOK` yok | Zon hesaba alinmaz | `BLOK=A` ekleyin |
| `TIP` yok veya tanimsiz | Zon hesaba alinmaz | Desteklenen TIP degerlerinden birini kullanin |
| Bagimsiz bolum TIP kodunda `BB` yok, Zon Numarasi da bos | Zon hesaba alinmaz | `BB=01` yazin veya Zon Numarasini doldurun; ortak/kat alanlarinda BB gerekmez |
| Zon alani 0 veya negatif | Zon hesaba alinmaz | Zon sinirini ve alan hesaplamasini duzeltin |
| `ODA=3+1` gibi metin | Oda degeri guvenilir okunmaz | Yalnizca tam sayi kullanin |
| Ayni dairede `BB=01` ve `BB=1` | Iki ayri BB olusur | Tek numaralama standardi kullanin |
| `BLOK=A` ve `BLOK=A BLOK` | Iki ayri blok olusur | Sadece `A`, `B`, `C` kullanin |
| Yanlis ana kat | Panelde yanlis kat gorunur | Zonun Home Story bilgisini duzeltin |
| Ayni alan icin kopya RH zonu | Alan iki kez toplanir | Kopya zonu silin veya RH kodunu kaldirin |

Durum satirinda **taranan zon** sayisi ile **RH zonu aktarildi** sayisi farkli olabilir. RH kodu olmayan normal zonlar bu farkin bir parcasidir. Mevcut surum ayrintili hata listesini panelde gostermedigi icin, beklenen bir RH zonu aktarilmadiysa yukaridaki kontroller uygulanmalidir.

## 15. Ortak, yapi insaat, emsal ve siginak zonlari

Ruhsat Hesap 0.5.9 ile asagidaki alan kodlari da otomatik aktarilir. Bu zonlarda `BB` zorunlu degildir; `BLOK` ve `TIP` yazilmali, `ORTAK` disindaki alanlar dogru ana kata yerlestirilmelidir. Kat bilgisi zon koduna yazilmaz; zonun Archicad'deki ana kati otomatik okunur (bkz. Bolum 7).

| Kod | Panelde yazildigi yer | Davranis |
|---|---|---|
| `TIP=ORTAK` | Bagimsiz Bolumler > Toplam ortak alan | Tum bloklardaki ORTAK zonlari proje toplaminda toplanir ve genel brut dagitiminda kullanilir |
| `TIP=MERDIVEN` | Yapi Insaat Alani > MERDIVEN | Zonun blogu ve ana kati esas alinir |
| `TIP=HOL` | Yapi Insaat Alani > HOL | Toplam kat holudur; Emsal Hesabi altindaki `kat_holu` kaleminden bagimsizdir |
| `TIP=EMSAL` | Emsal Hesabi > Emsal alan | Zonun blogu ve ana kati esas alinir |
| `TIP=EMSAL_DISI` | Emsal Hesabi > Emsal disi | Zonun blogu ve ana kati esas alinir |
| `TIP=SIGINAK` | Yapi Insaat Alani > SIGINAK ve Siginak Hesabi > Projede ayrilan net siginak | Zonun blogu ve ana kati esas alinir; tum siginak zonlari ayrilan net alanda toplanir |
| `TIP=SACAK` | Yapi Insaat Alani > SACAK | Zonun blogu ve ana kati esas alinir; `HESAP=EMSAL` ile hedef degistirilebilir (asagida) |
| `TIP=ASANSOR` | Yapi Insaat Alani > ASANSOR | Zonun blogu ve ana kati esas alinir; `HESAP=EMSAL` ile hedef degistirilebilir (asagida) |

Ornekler:

```text
RH|BLOK=A|TIP=ORTAK|MAHAL=ORTAK_ALAN
RH|BLOK=A|TIP=MERDIVEN|MAHAL=MERDIVEN
RH|BLOK=A|TIP=HOL|MAHAL=KAT_HOLU
RH|BLOK=A|TIP=EMSAL|MAHAL=EMSAL_ALANI
RH|BLOK=A|TIP=EMSAL_DISI|MAHAL=EMSAL_DISI_ALAN
RH|BLOK=A|TIP=SIGINAK|MAHAL=SIGINAK
RH|BLOK=A|TIP=SACAK|MAHAL=SACAK
RH|BLOK=A|TIP=ASANSOR|MAHAL=ASANSOR
```

Onemli: `TIP=HOL`, Yapi Insaat Alani tablosundaki toplam hol alanidir. Emsal hesabina konu `%30 kat holu` ile otomatik olarak esitlenmez. Bu iki veri birbirinden bagimsiz kalir.

Zon aktarimi tekrar calistirildiginda onceki otomatik katkilar yenileriyle degistirilir; ayni alan iki kez eklenmez. Panelde yapilan manuel ilaveler korunur. Bir RH zonu silinirse sonraki aktarimda yalnizca o zon kaynaginin onceki katkisi kaldirilir.

### HESAP anahtari: Emsal Hesabi %30 tablosuna otomatik aktarim

`TIP=MERDIVEN`, `TIP=HOL`, `TIP=SACAK` ve `TIP=ASANSOR` varsayilan olarak yalnizca **Yapi Insaat Alani** tablosunu doldurur; Emsal Hesabi sekmesindeki **%30 istisna tablosu** (merdiven, sacak, kat_holu vb. sutunlar) elle girilmeye devam eder. Bu ayni alanin **Emsal Hesabi %30 tablosuna** otomatik yazilmasini isteyen projeler icin `HESAP=EMSAL` anahtari eklenir:

```text
RH|BLOK=A|HESAP=EMSAL|TIP=MERDIVEN
RH|BLOK=A|HESAP=EMSAL|TIP=HOL
RH|BLOK=A|HESAP=EMSAL|TIP=SACAK
RH|BLOK=A|HESAP=EMSAL|TIP=ASANSOR
```

| Anahtar | Kisa ad | Kabul edilen deger | Davranis |
|---|---|---|---|
| `HESAP` | `H` | `EMSAL` | `TIP` degerindeki alani Emsal Hesabi %30 tablosuna yazar |
| `HESAP` yok veya `EMSAL` disi bir deger | - | - | Varsayilan davranis: alan Yapi Insaat Alani tablosuna yazilir |

Kurallar:

- `HESAP` yalnizca `TIP=MERDIVEN`, `TIP=HOL`, `TIP=SACAK`, `TIP=ASANSOR` ve asagidaki serbest (ozel) `TIP` degerleri icin gecerlidir. `TIP=EMSAL` ve `TIP=EMSAL_DISI` zaten dogrudan Emsal Hesabi tablosuna yazar ve `HESAP`'tan etkilenmez. `TIP=SIGINAK` her zaman Yapi Insaat Alani ve Siginak Hesabi'na yazar; `HESAP=EMSAL` ile yonlendirilemez.
- Ayni fiziksel alani hem Yapi Insaat Alani'na hem %30 tablosuna yazdirmak icin **iki ayri zon** olusturun: biri `HESAP` olmadan (Yapi Insaat Alani), digeri `HESAP=EMSAL` ile (%30 tablosu). Tek bir zon her iki tabloyu birden doldurmaz.
- `HESAP=EMSAL|TIP=HOL`, %30 tablosunda **ayri bir `hol` sutunu** olusturur; mevcut `kat_holu` sutunuyla otomatik birlesmez. Bolum 15'teki bagimsizlik kurali boylece korunur.
- Hedef sutun panelde henuz yoksa (ornegin ilk kez `SACAK` kullaniliyorsa) aktarim sirasinda otomatik olarak eklenir; elle "Alan Basligi Ekle" yapmaya gerek yoktur.
- Kisa yazim da calisir: `RH|B=A|H=EMSAL|T=MERDIVEN`.

### Serbest (ozel) TIP degerleri: kendi alan kaleminizi tanimlayin

`MERDIVEN`, `HOL`, `SACAK`, `ASANSOR`, `SIGINAK` ve diger sabit anahtar kelimelerin disinda yazilan **her `TIP` degeri**, kendi adiyla yeni bir Yapi Insaat Alani / Emsal Hesabi %30 kalemi olusturur -- panelde "Alan Basligi Ekle" ile elle eklediginiz bir sutunla ayni mantikla:

```text
RH|BLOK=A|TIP=HAVUZ_KENARI
RH|BLOK=A|HESAP=EMSAL|TIP=HAVUZ_KENARI
```

- `TIP` degeri kucuk harfe cevrilir, bosluklar tek alt cizgiye (`_`) donusturulur; sonuc panel sutununun anahtaridir. `TIP=Havuz Kenari` ile `TIP=HAVUZ_KENARI` ayni `havuz_kenari` sutununu olusturur.
- `HESAP` yoksa alan Yapi Insaat Alani'na, `HESAP=EMSAL` ile Emsal Hesabi %30 tablosuna yazilir -- tipki `MERDIVEN`/`SACAK` gibi.
- Sutun panelde yoksa aktarimda otomatik olarak eklenir.
- Turkce karakterler ASCII'ye donusturulmez, oldugu gibi saklanir. Panelde "Alan Basligi Ekle" ile daha once elle yazilmis bir sutunla otomatik eslesmesini istiyorsaniz, `TIP` degerini panelde yazdiginiz etiketle **harfi harfine** ayni yazin; farkli yazim (ozellikle Turkce karakter kullanimindaki farklar) ayri bir sutun olusturur. Karisikligi onlemek icin ASCII ve alt cizgi kullanmaniz onerilir: `TIP=HAVUZ_KENARI`.
- Bu deger `NET`, `BRUT`, `EKLENTI_NET`, `EKLENTI_BRUT`, `BALKON`, `ORTAK`, `MERDIVEN`, `HOL`, `EMSAL`, `EMSAL_DISI`, `SIGINAK`, `SACAK` veya `ASANSOR` ile **birebir** eslesirse, o sabit anlam kazanir; serbest kalem olarak degil, standart TIP olarak islenir.

**`NITELIK` bu kodlarda kullanilmaz.** `NITELIK` (Bolum 3 ve 8) bagimsiz bolum niteligini (`MESKEN`, `OFIS`, `DUKKAN`...) tasir ve alan tipini belirlemez. `RH|BLOK=A|NITELIK=MERDIVEN` gecersiz bir zondur (`TIP` eksik oldugu icin hesaba alinmaz); dogru kod `RH|BLOK=A|TIP=MERDIVEN` veya `RH|BLOK=A|HESAP=EMSAL|TIP=MERDIVEN` bicimindedir.

## 16. Ofis standardi onerisi

Proje ekibinde su kurallari sabitleyin:

- Uzun anahtar bicimini kullanin.
- Bloklari `A`, `B`, `C` olarak adlandirin.
- BB numaralarinda iki haneli standardi koruyun: `01`, `02`, `03`.
- TIP degerlerini yalnizca desteklenen kodlardan secin: `NET`, `BRUT`, `BALKON`, `EKLENTI_NET`, `EKLENTI_BRUT`, `ORTAK`, `MERDIVEN`, `HOL`, `EMSAL`, `EMSAL_DISI`, `SIGINAK`, `SACAK`, `ASANSOR`.
- ODA degerini tam sayi girin.
- Mahal adlarini ASCII buyuk harfle yazin: `YATAK_ODASI_1`, `SALON`, `MUTFAK`.
- Nitelik sozlugunu proje basinda belirleyin: `MESKEN`, `OFIS`, `DUKKAN`, `DEPO`.
- Aktarimdan once Archicad Zon Numarasi, ana kat ve alan kontrollerini yapin.

### Kopyala-yapistir sablonlari

```text
RH|BLOK=A|BB=01|TIP=NET|ODA=3|MAHAL=SALON|NITELIK=MESKEN
RH|BLOK=A|BB=01|TIP=BRUT|ODA=3|MAHAL=DAIRE_BRUT|NITELIK=MESKEN
RH|BLOK=A|BB=01|TIP=BALKON|ODA=3|MAHAL=BALKON|NITELIK=MESKEN
RH|BLOK=A|BB=01|TIP=EKLENTI_NET|ODA=3|MAHAL=DEPO|NITELIK=MESKEN
RH|BLOK=A|BB=01|TIP=EKLENTI_BRUT|ODA=3|MAHAL=DEPO_BRUT|NITELIK=MESKEN
```

## 17. Son kontrol listesi

Aktarimdan once:

- [ ] Her hesap zonu `RH` ile basliyor.
- [ ] Her RH zonunda `BLOK` var.
- [ ] Her RH zonunda desteklenen bir `TIP` var.
- [ ] `BB` veya Archicad Zon Numarasi dolu.
- [ ] Ayni bagimsiz bolumde BLOK ve BB yazimi birebir ayni.
- [ ] Zonlar dogru ana katta.
- [ ] Zon alanlari sifirdan buyuk.
- [ ] Brut, net, balkon ve eklenti zonlari dogru sinirlari temsil ediyor.
- [ ] Kopya veya cakisan RH zonu yok.

Aktarimdan sonra:

- [ ] Taranan ve aktarilan zon sayilari beklenen duzeyde.
- [ ] Her blok ve BB panelde bir kez gorunuyor.
- [ ] Kat bilgisi dogru.
- [ ] Net ve brut alanlar Archicad zon toplamiyla uyumlu.
- [ ] Eklenti ve balkon alanlari dogru sutunlarda.
- [ ] Oda sayisi ve nitelik dogru.
- [ ] Proje verisi PLN icine kaydedildi.

---

**Onerilen ana sablon:**

```text
RH|BLOK={A}|BB={01}|TIP={NET}|ODA={3}|MAHAL={SALON}|NITELIK={MESKEN}
```
