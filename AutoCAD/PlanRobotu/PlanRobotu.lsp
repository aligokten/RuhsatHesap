;;; ===========================================================================
;;;  PLAN ROBOTU  -  AutoCAD icin otomatik kat plani uretici
;;;  Planli Alanlar Imar Yonetmeligi (PAIY) asgari olculerine gore,
;;;  secilen dikdortgen alanin icine daireleri, mahalleri, merdiven evini,
;;;  kat holunu, kapilari ve pencereleri yerlestirir.
;;;
;;;  Kullanim :  APPLOAD ile PlanRobotu.lsp dosyasini yukleyin,
;;;              PLANROBOTU (kisa yol: PROBOT) komutunu calistirin,
;;;              dis kontur olan dikdortgen polyline'i secin,
;;;              acilan formu doldurup Tamam'a basin.
;;;
;;;  Dosya kodlamasi UTF-8'dir (AutoCAD 2021 ve sonrasi).
;;; ===========================================================================

(vl-load-com)

(setq *pr-surum* "1.3")

;;; ---------------------------------------------------------------------------
;;;  1. YONETMELIK / TASARIM TABLOSU  (tum olculer METRE)
;;;  Degerleri yururlukteki PAIY metnine ve varsa belediyenizin
;;;  yonetmeligine gore buradan guncelleyebilirsiniz.
;;; ---------------------------------------------------------------------------

;; (kod  ad  bolge  asgariAlan(m2)  asgariDarKenar(m)  kapiGenisligi(m))
;; bolge: "CEPHE"  -> dis cepheye bakan, pencereli yasam mahalli
;;        "SERVIS" -> daire girisi tarafindaki islak / yardimci mahal
(setq *pr-mahal-tipleri*
  '(("SALON"  "SALON"        "CEPHE"  12.00 3.00 0.90)   ; PAIY Md.29 oturma odasi
    ("YATAK"  "YATAK ODASI"  "CEPHE"   9.00 2.50 0.90)   ; PAIY Md.29 yatak odasi
    ("MUTFAK" "MUTFAK"       "CEPHE"   3.30 1.50 0.90)   ; PAIY Md.29 mutfak
    ("BANYO"  "BANYO"        "SERVIS"  3.00 1.50 0.80)   ; PAIY Md.29 banyo
    ("WC"     "WC"           "SERVIS"  1.20 1.00 0.80)   ; PAIY Md.29 hela
    ("KILER"  "KİLER"        "SERVIS"  0.00 0.90 0.70)   ; asgari yok, tasarim degeri
    ("ANTRE"  "ANTRE"        "SERVIS"  0.00 1.20 1.00))) ; antre genisligi / daire giris kapisi

;; Formdaki mahal alanlarinin sirasi (daire tanimi bu sirada sayilardan olusur)
(setq *pr-form-kodlar* '("SALON" "YATAK" "MUTFAK" "BANYO" "WC" "KILER" "BALKON"))
(setq *pr-form-etiketler*
  '("Salon / Oturma odası" "Yatak odası" "Mutfak" "Banyo" "WC (hela)" "Kiler" "Balkon"))

(setq *pr-koridor*          1.10   ; daire ici koridor genisligi
      *pr-kat-holu*         1.50   ; kat holu (ortak hol) asgari genisligi
      *pr-merdiven-kol*     1.20   ; konut ortak merdiven kol genisligi
      *pr-merdiven-goz*     0.10   ; merdiven gozu
      *pr-riht-max*         0.18   ; azami riht yuksekligi
      *pr-basamak*          0.27   ; basamak genisligi
      *pr-asansor-en*       1.80   ; asansor kuyusu (kabin 1.20x1.50 icin)
      *pr-asansor-boy*      2.00
      *pr-asansor-kapi*     0.90
      *pr-balkon-derinlik*  1.20   ; daire icine gomulu balkon (loggia) net derinligi
      *pr-pencere-max*      1.50   ; azami pencere genisligi
      *pr-yazi-boy*         0.18)  ; mahal adi yazi yuksekligi (m)

;; Hazir sablonlar: (ad (SALON YATAK MUTFAK BANYO WC KILER BALKON))
(setq *pr-sablonlar*
  '(("Stüdyo (1+0)" (1 0 1 1 0 0 0))
    ("1+1"          (1 1 1 1 0 0 1))
    ("2+1"          (1 2 1 1 1 0 1))
    ("3+1"          (1 3 1 2 1 0 1))
    ("4+1"          (1 4 1 2 1 1 2))))

;; Katman adlari ve renkleri
(setq *pr-katmanlar*
  '(("PR-DUVAR" 7) ("PR-KAPI" 30) ("PR-PENCERE" 4) ("PR-MERDIVEN" 2)
    ("PR-YAZI" 7) ("PR-DAIRE" 1) ("PR-RAPOR" 8)))

;;; ---------------------------------------------------------------------------
;;;  2. GENEL YARDIMCILAR
;;; ---------------------------------------------------------------------------

(defun pr:v+ (a b) (list (+ (car a) (car b)) (+ (cadr a) (cadr b))))
(defun pr:v- (a b) (list (- (car a) (car b)) (- (cadr a) (cadr b))))
(defun pr:v* (a k) (list (* (car a) k) (* (cadr a) k)))
(defun pr:dot (a b) (+ (* (car a) (car b)) (* (cadr a) (cadr b))))
(defun pr:cross (a b) (- (* (car a) (cadr b)) (* (cadr a) (car b))))
(defun pr:len (a) (sqrt (pr:dot a a)))
(defun pr:birim (a / l)
  (setq l (pr:len a))
  (if (> l 1e-12) (pr:v* a (/ 1.0 l)) a))
(defun pr:dik (a) (list (- (cadr a)) (car a)))       ; 90 derece saat yonu tersi
(defun pr:ayni (a b) (< (pr:len (pr:v- a b)) 1e-6))
(defun pr:butlast (l) (reverse (cdr (reverse l))))

(defun pr:tavan (x / f)
  (setq f (fix x))
  (if (> x (+ f 1e-9)) (1+ f) f))

;; 12.345 -> "12,35"  (Turkce ondalik, iki basamak)
(defun pr:fmt (x / i k)
  (if (> (abs x) 1e7) (setq x (if (< x 0.0) -1e7 1e7)))   ; AutoLISP tamsayisi 32 bit
  (setq i (fix (+ (* (abs x) 100.0) 0.5))
        k (rem i 100))
  (strcat (if (< x -0.004999) "-" "")
          (itoa (/ i 100)) ","
          (if (< k 10) "0" "") (itoa k)))

(defun pr:p (k) (cadr (assoc k *pr-par*)))
(defun pr:tip (kod) (assoc kod *pr-mahal-tipleri*))
(defun pr:push (x) (setq *pr-prims* (cons x *pr-prims*)))
(defun pr:rapor (s) (setq *pr-rapor* (cons s *pr-rapor*)))

(defun pr:spec-say (spec kod / i)
  (setq i (vl-position kod *pr-form-kodlar*))
  (if i (nth i spec) 0))

(defun pr:spec-adi (spec)
  (strcat (itoa (pr:spec-say spec "YATAK")) "+" (itoa (pr:spec-say spec "SALON"))))

;; Cokgen alani (shoelace)
(defun pr:alan (pts / i n s a b)
  (setq i 0 n (length pts) s 0.0)
  (while (< i n)
    (setq a (nth i pts)
          b (nth (rem (1+ i) n) pts)
          s (+ s (pr:cross a b))
          i (1+ i)))
  (abs (/ s 2.0)))

;;; ---------------------------------------------------------------------------
;;;  3. MAHAL, ACIKLIK VE CIZIM ILKELLERI  (bina ic koordinatlari, metre)
;;;  Bina ic koordinat sistemi: orijin dis duvarin ic yuzunun kosesi,
;;;  x uzun kenar boyunca, y kisa kenar boyunca.
;;; ---------------------------------------------------------------------------

;; Cerceve: (P0 U V) -> yerel (u v) noktasini bina koordinatina cevirir
(defun pr:ub (fr u v)
  (pr:v+ (car fr) (pr:v+ (pr:v* (cadr fr) u) (pr:v* (caddr fr) v))))

(defun pr:dik4 (fr u0 v0 u1 v1)
  (list (pr:ub fr u0 v0) (pr:ub fr u1 v0) (pr:ub fr u1 v1) (pr:ub fr u0 v1)))

;; Mahal kaydi: (kod ad noktalar genislik derinlik daireNo etiketNoktasi)
(defun pr:mahal-ekle (kod ad pts w d no etk)
  (setq *pr-mahaller* (cons (list kod ad pts w d no etk) *pr-mahaller*)))

;; Aciklik kaydi: (tip pA pB n kalinlik)
;; pA-pB : acikligin mahal tarafindaki duvar yuzu, n : mahalin icine dogru birim vektor
;; tip   : "KAPI" (kanat + yay), "BOSLUK" (kapisiz gecis), "PENCERE"
(defun pr:aciklik (tip fr u0 u1 vYuz yon kal / pa pb n)
  (setq pa (pr:ub fr u0 vYuz)
        pb (pr:ub fr u1 vYuz)
        n  (pr:v* (caddr fr) yon))
  (setq *pr-acikliklar* (cons (list tip pa pb n kal) *pr-acikliklar*))
  (pr:aciklik-sembol tip pa pb n kal))

(defun pr:aciklik-sembol (tip pa pb n kal / pa2 pb2 w)
  (setq pa2 (pr:v- pa (pr:v* n kal))
        pb2 (pr:v- pb (pr:v* n kal))
        w   (pr:len (pr:v- pb pa)))
  (cond
    ((= tip "PENCERE")
     (pr:push (list "LN" "PR-PENCERE" pa pa2))
     (pr:push (list "LN" "PR-PENCERE" pb pb2))
     (pr:push (list "LN" "PR-PENCERE" pa pb))
     (pr:push (list "LN" "PR-PENCERE"
                    (pr:v- pa (pr:v* n (/ kal 2.0))) (pr:v- pb (pr:v* n (/ kal 2.0)))))
     (pr:push (list "LN" "PR-PENCERE" pa2 pb2)))
    (T
     (pr:push (list "LN" "PR-DUVAR" pa pa2))
     (pr:push (list "LN" "PR-DUVAR" pb pb2))
     (if (= tip "KAPI")
       (progn
         (pr:push (list "LN" "PR-KAPI" pa (pr:v+ pa (pr:v* n w))))
         (pr:push (list "ARC" "PR-KAPI" pa (pr:v+ pa (pr:v* n w)) pb)))))))

(defun pr:aciklik-yuzleri (o / pa pb n k)
  (setq pa (cadr o) pb (caddr o) n (cadddr o) k (nth 4 o))
  (list (list pa pb) (list (pr:v- pa (pr:v* n k)) (pr:v- pb (pr:v* n k)))))

;; p noktasinin a-b dogrusuna uzakligi
(defun pr:dogru-uzaklik (p a b / ab)
  (setq ab (pr:v- b a))
  (abs (/ (pr:cross ab (pr:v- p a)) (pr:len ab))))

;; a->b kenarindan aciklik bosluklari cikarilmis parcalar: ((p q) ...)
(defun pr:kenar-parcalari (a b / l dir araliklar p q t1 t2 tmp cur res)
  (setq l (pr:len (pr:v- b a))
        dir (pr:birim (pr:v- b a))
        araliklar nil)
  (foreach o *pr-acikliklar*
    (foreach f (pr:aciklik-yuzleri o)
      (setq p (car f) q (cadr f))
      (if (and (< (pr:dogru-uzaklik p a b) 1e-6)
               (< (pr:dogru-uzaklik q a b) 1e-6))
        (progn
          (setq t1 (pr:dot (pr:v- p a) dir)
                t2 (pr:dot (pr:v- q a) dir))
          (if (> t1 t2) (setq tmp t1 t1 t2 t2 tmp))
          (setq t1 (max t1 0.0) t2 (min t2 l))
          (if (> (- t2 t1) 1e-6)
            (setq araliklar (cons (list t1 t2) araliklar)))))))
  (setq araliklar (vl-sort araliklar '(lambda (x y) (< (car x) (car y))))
        cur 0.0
        res nil)
  (foreach r araliklar
    (if (> (car r) (+ cur 1e-6))
      (setq res (cons (list (pr:v+ a (pr:v* dir cur)) (pr:v+ a (pr:v* dir (car r)))) res)))
    (setq cur (max cur (cadr r))))
  (if (< cur (- l 1e-6))
    (setq res (cons (list (pr:v+ a (pr:v* dir cur)) b) res)))
  (reverse res))

;; Mahal sinirini, kapi/pencere bosluklari acilmis polyline'lar olarak cizer
(defun pr:poligon-ciz (pts lay / i n segs zincir zincirler ilk son)
  (setq i 0 n (length pts) segs nil)
  (while (< i n)
    (setq segs (append segs (pr:kenar-parcalari (nth i pts) (nth (rem (1+ i) n) pts)))
          i (1+ i)))
  (setq zincir nil zincirler nil)
  (foreach s segs
    (if (and zincir (pr:ayni (last zincir) (car s)))
      (setq zincir (append zincir (list (cadr s))))
      (progn
        (if zincir (setq zincirler (cons zincir zincirler)))
        (setq zincir (list (car s) (cadr s))))))
  (if zincir (setq zincirler (cons zincir zincirler)))
  (setq zincirler (reverse zincirler))
  (if (and (> (length zincirler) 1)
           (pr:ayni (last (last zincirler)) (car (car zincirler))))
    (setq ilk (car zincirler)
          son (last zincirler)
          zincirler (cons (append son (cdr ilk)) (cdr (pr:butlast zincirler)))))
  (foreach z zincirler
    (if (and (= (length zincirler) 1) (pr:ayni (car z) (last z)))
      (pr:push (list "PL" lay (pr:butlast z) T))
      (pr:push (list "PL" lay z nil)))))

;;; ---------------------------------------------------------------------------
;;;  4. DAIRE ICI YERLESIM
;;;  Daire, yerel (u v) sisteminde genW x derD dikdortgenidir; giris v=0 kenarindadir.
;;;    v = 0 .. Ds            : SERVIS bandi (antre, islak hacimler; yan cephe
;;;                             disaridaysa uclarda pencereli yatak odasi)
;;;    v = Ds+ti .. +Wk       : daire ici koridor (son kapidan sonra biter)
;;;    v = Ds+2ti+Wk .. D     : CEPHE bandi (salon, mutfak, yatak odalari)
;;;  Olasi tum yerlesimler (mutfak yeri, yan cepheye alinan yatak odalari,
;;;  servis bandi derinligi) TASARIM PROFILI'ne gore puanlanir; en iyisi cizilir.
;;; ---------------------------------------------------------------------------

;; TASARIM PROFILI - ornek konut planlarindan cikarilan degerler
;; (kod  idealAlan(m2)  idealOran  azamiOran  buyumeAgirligi)
;; oran = uzun kenar / kisa kenar. Buyume agirligi: fazla alanin hangi mahale
;; ne kadar verilecegi (salon en cok, islak hacimler en az).
;; SALON ideali yatak sayisina gore ayarlanir: 18 + 3 x yatak (ornek: 2+1 -> 24 m2)
;; Ilk yatak odasi (ebeveyn) 13,5 m2, digerleri 11 m2 hedeflenir.
(setq *pr-profil*
  '(("SALON"  24.0 1.45 2.00 3.0)
    ("YATAK"  12.0 1.25 1.70 1.5)
    ("MUTFAK"  9.0 1.60 2.50 1.0)
    ("BANYO"   5.0 1.40 2.00 0.3)
    ("WC"      2.2 1.60 2.40 0.1)
    ("KILER"   2.5 1.60 3.00 0.2)
    ("ANTRE"   4.5 1.80 3.50 0.4)))
(setq *pr-sirkulasyon-hedef* 0.12)   ; antre+koridor / daire alani hedefi
(setq *pr-islak-derinlik* 2.60)      ; islak hacimler koridor tarafinda bu derinlikte kalir;
                                     ; giris duvari boyunca kalan serit VESTIYER / DEPO olur

(defun pr:islak-mi (o) (member (car o) '("WC" "BANYO" "KILER")))

;; Servis bandi derinligi ds iken islak hacim derinligi
(defun pr:islak-der (ds / dw)
  (setq dw (min ds *pr-islak-derinlik*))
  (if (< (- ds dw (pr:p "TI")) 1.0) ds dw))

(defun pr:prf (kod i) (nth i (assoc kod *pr-profil*)))
(defun pr:kare (x) (* x x))

;; oda: (kod ad asgariAlan asgariKenar kapi balkonlu idealAlan)
(defun pr:oda (kod ad / r)
  (setq r (pr:tip kod))
  (list kod ad (nth 3 r) (nth 4 r) (nth 5 r) nil (pr:prf kod 1)))

(defun pr:oda-balkonlu (o) (pr:liste-degistir o 5 T))

(defun pr:adlandir (kod n i)
  (if (> n 1) (strcat (cadr (pr:tip kod)) " " (itoa i)) (cadr (pr:tip kod))))

(defun pr:oda-listesi (spec kod / res n i o)
  (setq res nil n (pr:spec-say spec kod) i 1)
  (repeat n
    (setq o (pr:oda kod (pr:adlandir kod n i)))
    (cond ((= kod "YATAK") (setq o (pr:liste-degistir o 6 (if (= i 1) 13.5 11.0))))
          ((= kod "SALON") (setq o (pr:liste-degistir o 6 (+ 18.0 (* 3.0 (pr:spec-say spec "YATAK")))))))
    (setq res (append res (list o)) i (1+ i)))
  res)

;; Yerlesim adaylari: ((cephe servis mutfakCephede kalanBalkon) ...)
;; *pr-dis* = (solCepheDisarida sagCepheDisarida)
(defun pr:adaylar (spec / res yat ny sal mut isl mc sl sr cephe servis yL yR kalan balkon bo)
  (setq res nil
        yat (pr:oda-listesi spec "YATAK") ny (length yat)
        sal (pr:oda-listesi spec "SALON")
        mut (pr:oda-listesi spec "MUTFAK")
        isl (append (pr:oda-listesi spec "WC") (pr:oda-listesi spec "BANYO")
                    (pr:oda-listesi spec "KILER")))
  (foreach bo (if (> (pr:spec-say spec "BALKON") 0)
                '(("SALON" "YATAK" "MUTFAK") ("YATAK" "SALON" "MUTFAK"))
                '(("SALON")))
   (foreach mc '(T nil)
    (foreach sl '(nil T)
      (foreach sr '(nil T)
        (if (and (or mc mut)
                 (or (not sl) (and (car *pr-dis*) (>= ny 1)))
                 (or (not sr) (and (cadr *pr-dis*) (>= ny (if sl 2 1)))))
          (progn
            (setq kalan yat yL nil yR nil)
            (if sr (setq yR (last kalan) kalan (pr:butlast kalan)))
            (if sl (setq yL (last kalan) kalan (pr:butlast kalan)))
            (setq cephe (append sal (if mc mut nil) kalan)
                  servis (append (if yL (list yL) nil)
                                 (list (pr:oda "ANTRE" "ANTRE"))
                                 isl
                                 (if mc nil mut)
                                 (if yR (list yR) nil))
                  balkon (pr:spec-say spec "BALKON"))
            ;; balkonlar oncelik sirasina gore cephe odalarina verilir
            (foreach kod bo
              (setq cephe
                (mapcar '(lambda (o)
                           (if (and (> balkon 0) (= (car o) kod) (not (nth 5 o)))
                             (progn (setq balkon (1- balkon)) (pr:oda-balkonlu o))
                             o))
                        cephe)))
            (if cephe (setq res (cons (list cephe servis mc balkon) res)))))))))
  (reverse res))

;; Balkonlu odanin net derinligi
(defun pr:oda-derinlik (o bantDerinlik)
  (if (nth 5 o)
    (- bantDerinlik *pr-balkon-derinlik* (pr:p "TD"))
    bantDerinlik))

;; Odanin asgari genisligi (derinlik yetersizse nil)
(defun pr:oda-gen (o derinlik / a s)
  (setq a (nth 2 o) s (nth 3 o))
  (if (< derinlik (- s 1e-9))
    nil
    (max s (/ a derinlik))))

;; Profil hedef genisligi: ideal alan, ideal oran bandina kirpilir
(defun pr:oda-hedef (o derinlik / m oran h)
  (setq m (pr:oda-gen o derinlik)
        oran (pr:prf (car o) 2)
        h (/ (nth 6 o) derinlik))
  (setq h (max h (/ derinlik oran)))
  (setq h (min h (* derinlik oran)))
  (max m h))

;; Bant toplam asgari genisligi (araya ic duvarlar dahil) ya da nil
(defun pr:bant-gen (odalar derinlik / top gw ok)
  (setq top 0.0 ok T)
  (foreach o odalar
    (setq gw (pr:oda-gen o (pr:oda-derinlik o derinlik)))
    (if gw (setq top (+ top gw)) (setq ok nil)))
  (cond ((not ok) nil)
        ((null odalar) 0.0)
        (T (+ top (* (1- (length odalar)) (pr:p "TI"))))))

;; En dar daire genisligini veren servis bandi derinligi: (gerekliW Ds) ya da nil
(defun pr:daire-cozum (cephe servis derD / ti wk ds wf ws en iyi)
  (setq ti (pr:p "TI") wk *pr-koridor* ds 0.0 iyi nil)
  (foreach o servis (setq ds (max ds (nth 3 o))))
  (while (<= (+ ds wk ti ti) (+ derD 1e-9))
    (setq wf (pr:bant-gen cephe (- derD ds wk ti ti))
          ws (pr:bant-gen servis ds))
    (if (and wf ws)
      (progn
        (setq en (max wf ws))
        (if (or (null iyi) (< en (- (car iyi) 1e-9)))
          (setq iyi (list en ds)))))
    (setq ds (+ ds 0.05)))
  iyi)

;; Daire icin gereken asgari genislik (sigmiyorsa cok buyuk sayi)
(defun pr:daire-gerekli (spec derD / en c)
  (setq en 1e9)
  (foreach a (pr:adaylar spec)
    (setq c (pr:daire-cozum (car a) (cadr a) derD))
    (if c (setq en (min en (car c)))))
  en)

;; Fazla genisligi agirliklara gore, tavanlara takilmadan dagitir
(defun pr:dagit (gen agirlik tavan fazla / topAg yeni)
  (repeat (1+ (length gen))
    (if (> fazla 1e-6)
      (progn
        (setq topAg 0.0)
        (mapcar '(lambda (g a tv) (if (< g (- tv 1e-6)) (setq topAg (+ topAg a)))) gen agirlik tavan)
        (if (> topAg 0.0)
          (setq yeni (mapcar '(lambda (g a tv / e)
                                (if (< g (- tv 1e-6))
                                  (progn (setq e (+ g (* fazla (/ a topAg)))) (min e tv))
                                  g))
                             gen agirlik tavan)
                fazla (- fazla (- (apply '+ yeni) (apply '+ gen)))
                gen yeni)))))
  ;; tum odalar tavanda ise kalan fazlalik geri dondurulur
  (list gen fazla))

;; Bant genislikleri: once asgari, sonra profil hedefi, sonra buyume agirliklari.
;; derinlikler: her odanin net derinligi. Sigmazsa nil (zorla=T ise orantili kucultur).
(defun pr:bant-dagit (odalar derinlikler avail zorla / mins hed topM topH k r gen)
  (setq mins (mapcar '(lambda (o dd) (pr:oda-gen o dd)) odalar derinlikler))
  (cond
    ((member nil mins)
     (if zorla (mapcar '(lambda (o) (* (nth 3 o) 1.0)) odalar) nil))
    ((> (setq topM (apply '+ mins)) (+ avail 1e-6))
     (if zorla (mapcar '(lambda (m) (* m (/ avail topM))) mins) nil))
    (T
     (setq hed (mapcar '(lambda (o dd) (pr:oda-hedef o dd)) odalar derinlikler)
           topH (apply '+ hed))
     (if (>= topH avail)
       (progn
         (setq k (if (> (- topH topM) 1e-9) (/ (- avail topM) (- topH topM)) 0.0))
         (mapcar '(lambda (m h) (+ m (* (- h m) k))) mins hed))
       (progn
         ;; once azami orana kadar buyut, artan hala varsa oranlari zorla
         (setq r (pr:dagit hed
                           (mapcar '(lambda (o) (pr:prf (car o) 4)) odalar)
                           (mapcar '(lambda (o dd h) (max h (* dd (pr:prf (car o) 3)))) odalar derinlikler hed)
                           (- avail topH)))
         (setq gen (car r))
         (if (> (cadr r) 1e-6)
           (setq gen (car (pr:dagit gen
                                    (mapcar '(lambda (o) (pr:prf (car o) 4)) odalar)
                                    (mapcar '(lambda (g) 1e9) gen)
                                    (cadr r)))))
         gen)))))

;; Tek odanin profile uzakligi (kucuk = iyi)
(defun pr:oda-puan (o gw dd / a r)
  (setq a (* gw dd)
        r (/ (max gw dd) (max 0.01 (min gw dd))))
  (+ (pr:kare (max 0.0 (- r (pr:prf (car o) 2))))
     (* 10.0 (pr:kare (max 0.0 (- r (pr:prf (car o) 3)))))
     (* 1.5 (pr:kare (/ (- a (nth 6 o)) (nth 6 o))))))

;; Koridorun bitebilecegi en kucuk u: son kapilarin otesi
(defun pr:koridor-sonu (cephe wf servis ws / ti u ue)
  (setq ti (pr:p "TI") u 0.0 ue 0.0)
  (mapcar '(lambda (o gw) (setq ue (max ue (+ u 0.20 (nth 4 o))) u (+ u gw ti))) cephe wf)
  (setq u 0.0)
  (mapcar '(lambda (o gw) (setq ue (max ue (+ u 0.20 (nth 4 o)) (if (= (car o) "ANTRE") (+ u gw) 0.0))
                                u (+ u gw ti)))
          servis ws)
  ue)

;; Antrenin u araligi
(defun pr:antre-araligi (servis ws / ti u res)
  (setq ti (pr:p "TI") u 0.0 res nil)
  (mapcar '(lambda (o gw) (if (= (car o) "ANTRE") (setq res (list u (+ u gw)))) (setq u (+ u gw ti)))
          servis ws)
  res)

;; Bir aday + servis derinligi icin cozum: (puan aday ds wf ws ue) ya da nil
(defun pr:aday-coz (aday genW derD ds / ti wk df cephe servis derF derS wf ws puan ar ue
                                       sirk net dw)
  (setq ti (pr:p "TI") wk *pr-koridor*
        df (- derD ds wk ti ti)
        cephe (car aday) servis (cadr aday))
  (setq derF (mapcar '(lambda (o) (pr:oda-derinlik o df)) cephe)
        dw (pr:islak-der ds)
        derS (mapcar '(lambda (o) (if (pr:islak-mi o) dw ds)) servis))
  (setq wf (pr:bant-dagit cephe derF (- genW (* (1- (length cephe)) ti)) nil)
        ws (pr:bant-dagit servis derS (- genW (* (1- (length servis)) ti)) nil))
  (if (and wf ws)
    (progn
      (setq ar (pr:antre-araligi servis ws)
            ue (pr:koridor-sonu cephe wf servis ws))
      ;; daire giris kapisi ortak hole acilabilmeli
      (if (<= (+ (car ar) 0.10 (nth 4 (pr:tip "ANTRE"))) (+ *pr-giris-azami* 1e-6))
        (progn
          ;; koridor sonrasi son odaya katilan serit en fazla 3 m olur
          (setq ue (max ue (- genW ti 3.0)))
          (if (< (- genW ue ti) 0.60) (setq ue genW))
          (setq puan 0.0)
          (mapcar '(lambda (o gw dd) (setq puan (+ puan (pr:oda-puan o gw dd)))) cephe wf derF)
          (mapcar '(lambda (o gw dd) (if (/= (car o) "ANTRE") (setq puan (+ puan (pr:oda-puan o gw dd)))))
                  servis ws derS)
          (setq net (* genW derD)
                sirk (+ (* (- (cadr ar) (car ar)) (+ ds ti)) (* ue wk)))
          (setq puan (+ puan (* 10.0 (max 0.0 (- (/ sirk net) *pr-sirkulasyon-hedef*)))))
          (list puan aday ds wf ws ue dw))))))

;; En iyi yerlesim; hic sigmiyorsa en dar adayi orantili kucultur
(defun pr:daire-plan (spec genW derD / ti wk adaylar iyi c ds dsMin en enA cephe servis df)
  (setq ti (pr:p "TI") wk *pr-koridor* iyi nil
        adaylar (pr:adaylar spec))
  (foreach a adaylar
    (setq dsMin 0.0)
    (foreach o (cadr a) (setq dsMin (max dsMin (nth 3 o))))
    (setq ds dsMin)
    (while (< (+ ds wk ti ti 1.0) derD)
      (setq c (pr:aday-coz a genW derD ds))
      (if (and c (or (null iyi) (< (car c) (car iyi)))) (setq iyi c))
      (setq ds (+ ds 0.10))))
  (if iyi
    iyi
    (progn
      ;; zorunlu (uygunsuz) yerlesim: en az genislik isteyen aday
      (setq en nil)
      (foreach a adaylar
        (setq c (pr:daire-cozum (car a) (cadr a) derD))
        (if (and c (or (null en) (< (car c) (car en)))) (setq en c enA a)))
      (if en
        (progn
          (setq ds (cadr en) cephe (car enA) servis (cadr enA)
                df (- derD ds wk ti ti))
          (list 1e9 enA ds
                (pr:bant-dagit cephe (mapcar '(lambda (o) (pr:oda-derinlik o df)) cephe)
                               (- genW (* (1- (length cephe)) ti)) T)
                (pr:bant-dagit servis (mapcar '(lambda (o) ds) servis)
                               (- genW (* (1- (length servis)) ti)) T)
                genW ds))))))

;; Dusey (u sabit) yuzlere aciklik koymak icin cerceveyi devrik kullanir
(defun pr:devrik (fr) (list (car fr) (caddr fr) (cadr fr)))

(defun pr:pencere (fr u0 u1 vYuz yon kal / gw m)
  (setq gw (min *pr-pencere-max* (- (- u1 u0) 0.60)))
  (if (>= gw 0.60)
    (progn
      (setq m (/ (+ u0 u1) 2.0))
      (pr:aciklik "PENCERE" fr (- m (/ gw 2.0)) (+ m (/ gw 2.0)) vYuz yon kal))))

;; Kapiyi oda kenarinin basina (koseden 10 cm) yerlestirir
(defun pr:kapi-ekle (fr u0 u1 vYuz yon kal genislik / a gw)
  (setq gw (min genislik (- (- u1 u0) 0.20)))
  (if (> gw 0.40)
    (progn
      (setq a (+ u0 0.10))
      (pr:aciklik "KAPI" fr a (+ a gw) vYuz yon kal))))

;; Ardisik ayni noktalari ayiklar
(defun pr:tekille (pts / res)
  (setq res nil)
  (foreach p pts
    (if (or (null res) (not (pr:ayni p (car res)))) (setq res (cons p res))))
  (if (and (> (length res) 1) (pr:ayni (car res) (last res))) (setq res (cdr res)))
  (reverse res))

;; Daireyi verilen cerceveye yerlestirir
(defun pr:daire-yerlestir (no spec fr genW derD / sec aday cephe servis ds df ti tp td wk
                                            v0 u wf ws o gw bal ue frT disL disR son ilk
                                            ar cv0 cv1 ust pts vUst dw iu0 iu1)
  (setq ti (pr:p "TI") tp (pr:p "TP") td (pr:p "TD") wk *pr-koridor*
        disL (car *pr-dis*) disR (cadr *pr-dis*) frT (pr:devrik fr))
  (setq sec (pr:daire-plan spec genW derD))
  (if (null sec)
    (progn
      (setq *pr-uyarilar*
             (cons (strcat "DAİRE " (itoa no) ": derinlik (" (pr:fmt derD)
                           " m) mahalleri yerleştirmeye yetmiyor - daire çizilemedi.") *pr-uyarilar*))
      (setq *pr-hata-sayisi* (1+ *pr-hata-sayisi*)))
    (progn
      (setq aday (cadr sec) ds (caddr sec) wf (cadddr sec) ws (nth 4 sec) ue (nth 5 sec)
            dw (nth 6 sec) iu0 nil iu1 nil
            cephe (car aday) servis (cadr aday)
            v0 (+ ds ti wk ti)
            df (- derD v0)
            cv0 (+ ds ti) cv1 (+ ds ti wk))
      (if (> (cadddr aday) 0)
        (setq *pr-uyarilar*
               (cons (strcat "DAİRE " (itoa no) ": " (itoa (cadddr aday))
                             " balkon için uygun oda bulunamadı.") *pr-uyarilar*)))
      ;; --- cephe bandi
      (setq u 0.0 ilk T)
      (foreach o cephe
        (setq gw (car wf) wf (cdr wf) son (null wf))
        (setq vUst (if (nth 5 o) (- derD *pr-balkon-derinlik* td) derD))
        (if (and son (< ue (- genW 1e-6)))
          ;; koridorun bittigi yerden sonrasi son odaya katilir (L oda)
          (setq pts (pr:tekille
                      (list (pr:ub fr u v0) (pr:ub fr (+ ue ti) v0) (pr:ub fr (+ ue ti) cv0)
                            (pr:ub fr genW cv0) (pr:ub fr genW vUst) (pr:ub fr u vUst))))
          (setq pts (pr:dik4 fr u v0 (+ u gw) vUst)))
        (pr:mahal-ekle (car o) (cadr o) pts gw (- vUst v0) no
                       (pr:ub fr (+ u (/ gw 2.0)) (/ (+ v0 vUst) 2.0)))
        (if (nth 5 o)
          (progn
            (setq bal (- derD *pr-balkon-derinlik*))
            (pr:mahal-ekle "BALKON" "BALKON" (pr:dik4 fr u bal (+ u gw) derD)
                           gw *pr-balkon-derinlik* no (pr:ub fr (+ u (/ gw 2.0)) (/ (+ bal derD) 2.0)))
            (pr:kapi-ekle fr u (+ u gw) vUst -1 td 0.90))
          (pr:pencere fr u (+ u gw) derD -1 td))
        ;; kose odalara yan cephe penceresi
        (if (and ilk disL) (pr:pencere frT v0 vUst 0.0 1 td))
        (if (and son disR) (pr:pencere frT v0 vUst genW -1 td))
        (pr:kapi-ekle fr u (+ u gw) v0 1 ti (nth 4 o))
        (setq u (+ u gw ti) ilk nil))
      ;; --- servis bandi
      (setq u 0.0 ar (pr:antre-araligi servis ws) ilk T)
      (foreach o servis
        (setq gw (car ws) ws (cdr ws) son (null ws))
        (if (= (car o) "ANTRE")
          (progn
            (setq pts (pr:tekille
                        (list (pr:ub fr (car ar) 0.0) (pr:ub fr (cadr ar) 0.0) (pr:ub fr (cadr ar) cv0)
                              (pr:ub fr ue cv0) (pr:ub fr ue cv1) (pr:ub fr 0.0 cv1)
                              (pr:ub fr 0.0 cv0) (pr:ub fr (car ar) cv0))))
            (pr:mahal-ekle "ANTRE" "ANTRE / KORİDOR" pts gw ds no
                           (pr:ub fr (+ u (/ gw 2.0)) (/ ds 2.0)))
            ;; daire giris kapisi (ortak hole acilir, kanat antreye)
            (pr:kapi-ekle fr u (+ u gw) 0.0 1 tp (nth 4 o)))
          (if (and (pr:islak-mi o) (< dw (- ds 1e-6)))
            ;; islak hacim koridor tarafinda, arkasinda vestiyer seridi
            (progn
              (if (null iu0) (setq iu0 u))
              (setq iu1 (+ u gw))
              (pr:mahal-ekle (car o) (cadr o) (pr:dik4 fr u (- ds dw) (+ u gw) ds)
                             gw dw no (pr:ub fr (+ u (/ gw 2.0)) (- ds (/ dw 2.0))))
              (pr:kapi-ekle fr u (+ u gw) ds -1 ti (nth 4 o)))
          (progn
            (pr:mahal-ekle (car o) (cadr o) (pr:dik4 fr u 0.0 (+ u gw) ds)
                           gw ds no (pr:ub fr (+ u (/ gw 2.0)) (/ ds 2.0)))
            (pr:kapi-ekle fr u (+ u gw) ds -1 ti (nth 4 o))
            ;; yan cepheye alinan yatak odasi penceresi
            (if (= (car o) "YATAK")
              (cond ((and ilk disL) (pr:pencere frT 0.0 ds 0.0 1 td))
                    ((and son disR) (pr:pencere frT 0.0 ds genW -1 td)))))))
        (setq u (+ u gw ti) ilk nil))
      ;; --- vestiyer / depo seridi (antreden girilir)
      (if iu0
        (progn
          (setq ust (- ds dw ti))
          (pr:mahal-ekle "DEPO" "VESTİYER / DEPO" (pr:dik4 fr iu0 0.0 iu1 ust)
                         (- iu1 iu0) ust no (pr:ub fr (/ (+ iu0 iu1) 2.0) (/ ust 2.0)))
          (pr:kapi-ekle frT 0.0 ust iu0 1 ti 0.80)))
      ;; --- daire etiketi (koridorda)
      (pr:push (list "TX" "PR-DAIRE" (pr:ub fr (/ ue 2.0) (+ ds ti (/ wk 2.0))) 0.22
                     (strcat "DAİRE " (itoa no) " (" (pr:spec-adi spec) ")") 0.0))
      (setq *pr-daireler* (cons (list no spec (if (caddr aday) "cephe" "iç") genW derD) *pr-daireler*)))))

;;; ---------------------------------------------------------------------------
;;;  5. ORTAK ALANLAR: MERDIVEN EVI, ASANSOR, KAT HOLU
;;; ---------------------------------------------------------------------------

;; Merdiven olculeri: (genislik kolBoyu basamakSayisiKolBasina riht toplamBoy)
(defun pr:merdiven-olcu (/ h n r kol)
  (setq h (pr:p "H")
        n (pr:tavan (/ h *pr-riht-max*)))
  (if (= (rem n 2) 1) (setq n (1+ n)))
  (setq r (/ n 2)
        kol *pr-merdiven-kol*)
  (list (+ kol *pr-merdiven-goz* kol)
        (* (1- r) *pr-basamak*)
        r
        (/ h n)
        (+ (* (1- r) *pr-basamak*) kol)))

(defun pr:cekirdek-gen (/ m)
  (setq m (pr:merdiven-olcu))
  (+ (car m) (if (pr:p "ASANSOR") (+ (pr:p "TI") *pr-asansor-en*) 0.0)))

(defun pr:cekirdek-boy (/ m)
  (setq m (pr:merdiven-olcu))
  (max (nth 4 m) (if (pr:p "ASANSOR") *pr-asansor-boy* 0.0)))

;; Merdiven evi + asansor. x: xc0..  y: yb (hol tarafi) .. ytop. holDuvar: hol ile arasindaki duvar
(defun pr:cekirdek (xc0 yb ytop holDuvar / m sw kol goz kosu r b i y x1 x2 ti xa fr ab)
  (setq m (pr:merdiven-olcu)
        sw (car m) kosu (cadr m) r (caddr m)
        kol *pr-merdiven-kol* goz *pr-merdiven-goz* b *pr-basamak*
        ti (pr:p "TI")
        fr (list (list 0.0 0.0) (list 1.0 0.0) (list 0.0 1.0)))
  (pr:mahal-ekle "MERDIVEN" "MERDİVEN" (pr:dik4 fr xc0 yb (+ xc0 sw) ytop)
                 sw (- ytop yb) 0 (list (+ xc0 (/ sw 2.0)) (+ yb kosu (/ kol 2.0))))
  ;; basamak cizgileri
  (setq i 0)
  (while (< i r)
    (setq y (+ yb (* i b)))
    (pr:push (list "LN" "PR-MERDIVEN" (list xc0 y) (list (+ xc0 kol) y)))
    (pr:push (list "LN" "PR-MERDIVEN" (list (+ xc0 kol goz) y) (list (+ xc0 sw) y)))
    (setq i (1+ i)))
  ;; merdiven gozu
  (pr:push (list "PL" "PR-MERDIVEN"
                 (pr:dik4 fr (+ xc0 kol) yb (+ xc0 kol goz) (+ yb kosu)) T))
  ;; ok cizgisi (cikis yonu)
  (setq x1 (+ xc0 (/ kol 2.0)) x2 (+ xc0 kol goz (/ kol 2.0)))
  (pr:push (list "PL" "PR-MERDIVEN"
                 (list (list x1 (+ yb 0.15)) (list x1 (+ yb kosu (/ kol 2.0)))
                       (list x2 (+ yb kosu (/ kol 2.0))) (list x2 (+ yb 0.30)))
                 nil))
  (pr:push (list "PL" "PR-MERDIVEN"
                 (list (list (- x2 0.10) (+ yb 0.45)) (list x2 (+ yb 0.30)) (list (+ x2 0.10) (+ yb 0.45)))
                 nil))
  ;; hol ile merdiven arasindaki kapisiz gecis
  (pr:aciklik "BOSLUK" fr (+ xc0 0.10) (- (+ xc0 sw) 0.10) yb 1 holDuvar)
  (if (< (- ytop yb) (- (nth 4 m) 1e-6))
    (setq *pr-uyarilar*
           (cons (strcat "Merdiven evi derinliği " (pr:fmt (- ytop yb)) " m, gereken "
                         (pr:fmt (nth 4 m)) " m.") *pr-uyarilar*)))
  ;; asansor
  (if (pr:p "ASANSOR")
    (progn
      (setq xa (+ xc0 sw ti)
            ab (min *pr-asansor-boy* (- ytop yb)))
      (pr:mahal-ekle "ASANSOR" "ASANSÖR" (pr:dik4 fr xa yb (+ xa *pr-asansor-en*) (+ yb ab))
                     *pr-asansor-en* ab 0
                     (list (+ xa (/ *pr-asansor-en* 2.0)) (+ yb (/ ab 2.0))))
      (pr:push (list "LN" "PR-MERDIVEN" (list xa yb) (list (+ xa *pr-asansor-en*) (+ yb ab))))
      (pr:push (list "LN" "PR-MERDIVEN" (list (+ xa *pr-asansor-en*) yb) (list xa (+ yb ab))))
      (pr:aciklik "BOSLUK" fr (+ xa (/ (- *pr-asansor-en* *pr-asansor-kapi*) 2.0))
                  (+ xa (/ (+ *pr-asansor-en* *pr-asansor-kapi*) 2.0)) yb 1 holDuvar)
      (if (> (- ytop (+ yb ab ti)) 0.30)
        (pr:mahal-ekle "SAFT" "TESİSAT ŞAFTI"
                       (pr:dik4 fr xa (+ yb ab ti) (+ xa *pr-asansor-en*) ytop)
                       *pr-asansor-en* (- ytop yb ab ti) 0
                       (list (+ xa (/ *pr-asansor-en* 2.0)) (/ (+ yb ab ti ytop) 2.0)))))))

;;; ---------------------------------------------------------------------------
;;;  6. KAT PLANI KURGUSU
;;;  Duzen A (1-2 daire): merdiven evi ortada (1 dairede yanda), daireler iki yanda.
;;;  Duzen B (3+ daire) : uzun eksende orta kat holu, merdiven evi arka sirada ortada,
;;;                       daireler on sirada ve arka siranin iki yaninda.
;;; ---------------------------------------------------------------------------

;; Sirali daireleri segmentlere bolup en dengeli dagilimi bulur.
;; segL: segment uzunluklari (3 adet). -> (enBuyukDoluluk (grup1 grup2 grup3))
;; Sirali daireleri segmentlere boler. Once sigan (doluluk <= 1) bolusumler,
;; aralarinda da daire buyukluklerini birbirine en yakin tutan secilir
;; (ornek planlarda ayni tip daireler esit genislikte). -> (doluluk (i j))
(defun pr:bolustur (gerek segL / n i j tp iyi g1 g2 g3 f dz anahtar)
  (setq n (length gerek) tp (pr:p "TP") iyi nil i 0)
  (while (<= i n)
    (setq j i)
    (while (<= j n)
      (setq g1 (pr:alt gerek 0 i) g2 (pr:alt gerek i j) g3 (pr:alt gerek j n))
      (setq f (max (pr:doluluk g1 (car segL) tp)
                   (pr:doluluk g2 (cadr segL) tp)
                   (pr:doluluk g3 (caddr segL) tp))
            dz (+ (pr:dengesizlik g1 (car segL) tp)
                  (pr:dengesizlik g2 (cadr segL) tp)
                  (pr:dengesizlik g3 (caddr segL) tp))
            anahtar (if (<= f 1.0) dz (+ 1e6 f)))
      (if (or (null iyi) (< anahtar (- (caddr iyi) 1e-9)))
        (setq iyi (list f (list i j) anahtar)))
      (setq j (1+ j)))
    (setq i (1+ i)))
  iyi)

;; Segmentteki dairelerin gereginden buyuk kalma olcusu; bos segment cezali
(defun pr:dengesizlik (g L tp / f)
  (cond ((null g) (if (> L 0.5) 1000.0 0.0))   ; kullanilmayan bos alan birakilmaz
        (T (setq f (pr:doluluk g L tp))
           (if (> f 1e-9) (* (length g) (pr:kare (- (/ 1.0 f) 1.0))) 1e6))))

(defun pr:alt (l a b / res i)
  (setq res nil i a)
  (while (< i b) (setq res (cons (nth i l) res) i (1+ i)))
  (reverse res))

(defun pr:doluluk (g L tp)
  (cond ((null g) 0.0)
        ((<= L 0.0) 1e9)
        ((>= (apply 'max g) 1e8) 1e9)          ; daire derinlige hic sigmiyor
        (T (/ (+ (apply '+ g) (* (1- (length g)) tp)) L))))

;; Bir segmentteki daireleri soldan saga yerlestirir
(defun pr:segment-yerlestir (nolar gerek x0 x1 yGiris vYon D / tp top avail x w fr)
  (setq tp (pr:p "TP"))
  (if nolar
    (progn
      (setq top (apply '+ gerek)
            avail (- x1 x0 (* (1- (length nolar)) tp))
            x x0)
      (foreach no nolar
        (setq w (* (car gerek) (/ avail top)) gerek (cdr gerek))
        (setq fr (list (list x yGiris) (list 1.0 0.0) (list 0.0 vYon))
              *pr-dis* (list (< x 1e-6) (> (+ x w) (- *pr-lin* 1e-6)))
              *pr-giris-azami* 1e9)
        (pr:daire-yerlestir no (nth (1- no) (pr:p "SPECS")) fr w D)
        (setq x (+ x w tp))))
    (if (> (- x1 x0) 0.5)
      (pr:mahal-ekle "BOS" "BOŞ ALAN"
                     (list (list x0 yGiris) (list x1 yGiris) (list x1 (+ yGiris (* vYon D)))
                           (list x0 (+ yGiris (* vYon D))))
                     (- x1 x0) D 0 (list (/ (+ x0 x1) 2.0) (+ yGiris (* vYon (/ D 2.0))))))))

;; Duzen A degerlendirmesi -> (doluluk ...) ya da nil
(defun pr:duzen-a (Lin Din / n specs ti tp cw cl yh xc0 D f)
  (setq n (pr:p "N") specs (pr:p "SPECS") ti (pr:p "TI") tp (pr:p "TP")
        cw (pr:cekirdek-gen) cl (pr:cekirdek-boy)
        yh (- Din ti cl))
  (if (and (<= n 2) (>= yh *pr-kat-holu*))
    (progn
      (setq xc0 (if (= n 2) (/ (- Lin cw) 2.0) (- Lin cw))
            D (- xc0 tp)
            f 0.0
            *pr-dis* (list T T) *pr-giris-azami* yh)
      (foreach s specs
        (setq f (max f (pr:doluluk (list (pr:daire-gerekli s D)) Din tp))))
      (list f xc0 D yh))))

;; Duzen B degerlendirmesi. Merdiven cekirdegi arka sirada, daire genisliklerini
;; en dengeli bolecek konuma kaydirilir (0,25 m adimla).
(defun pr:duzen-b (Lin Din / specs tp cw dr xc0 segL gerek b iyi)
  (setq specs (pr:p "SPECS") tp (pr:p "TP")
        cw (pr:cekirdek-gen)
        dr (/ (- Din *pr-kat-holu* tp tp) 2.0))
  (if (> dr 1.0)
    (progn
      (setq *pr-dis* (list nil nil) *pr-giris-azami* 1e9)
      (setq gerek (mapcar '(lambda (s) (pr:daire-gerekli s dr)) specs)
            xc0 0.0 iyi nil)
      (while (<= xc0 (+ (- Lin cw) 1e-6))
        (setq segL (list Lin (- xc0 tp) (- Lin xc0 cw tp))
              b (pr:bolustur gerek segL))
        ;; esitlikte ortaya yakin konum
        (if (or (null iyi)
                (< (caddr b) (- (car iyi) 1e-6))
                (and (< (abs (- (caddr b) (car iyi))) 1e-6)
                     (< (abs (- xc0 (/ (- Lin cw) 2.0))) (abs (- (cadr iyi) (/ (- Lin cw) 2.0))))))
          (setq iyi (list (caddr b) xc0 b)))
        (setq xc0 (+ xc0 0.25)))
      (setq b (caddr iyi) xc0 (cadr iyi))
      (list (car b) xc0 dr (cadr b) gerek))))

;; ANA URETICI: dis olculer Lx, Ly (metre). Basarida T doner.
(defun pr:plan-uret (Lx Ly / td ti tp Lin Din a b n nolar gerek xc0 dr yb ij cw cl yh D)
  (setq *pr-prims* nil *pr-acikliklar* nil *pr-mahaller* nil *pr-rapor* nil
        *pr-uyarilar* nil *pr-daireler* nil *pr-hata-sayisi* 0 *pr-duzen* nil)
  (setq td (pr:p "TD") ti (pr:p "TI") tp (pr:p "TP") n (pr:p "N")
        Lin (- Lx td td) Din (- Ly td td)
        *pr-lin* Lin
        cw (pr:cekirdek-gen) cl (pr:cekirdek-boy))
  (setq a (pr:duzen-a Lin Din) b (pr:duzen-b Lin Din))
  (cond
    ((and (or (null a) (>= (car a) 1e8)) (or (null b) (>= (car b) 1e8)))
     (pr:rapor (strcat "Seçilen alan (" (pr:fmt Lx) " x " (pr:fmt Ly) " m) bu daire sayısı ve mahal "
                       "programı için yetersiz: odalar asgari dar kenar ölçülerine sığmıyor."))
     (pr:rapor "Daire sayısını / mahal sayısını azaltın veya alanı büyütün.")
     nil)
    ((and a (or (null b) (<= (car a) (car b))))
     ;; ----- DUZEN A
     (setq *pr-duzen* (list "A" (car a))
           xc0 (cadr a) D (caddr a) yh (cadddr a))
     (pr:mahal-ekle "HOL" "KAT HOLÜ" (list (list xc0 0.0) (list (+ xc0 cw) 0.0)
                                           (list (+ xc0 cw) yh) (list xc0 yh))
                    cw yh 0 (list (+ xc0 (/ cw 2.0)) (/ yh 2.0)))
     (pr:cekirdek xc0 (+ yh ti) Din ti)
     (setq *pr-dis* (list T T) *pr-giris-azami* yh)
     ;; sol daire: giris x = xc0 - tp, u ekseni +y, v ekseni -x
     (pr:daire-yerlestir 1 (nth 0 (pr:p "SPECS"))
                         (list (list (- xc0 tp) 0.0) (list 0.0 1.0) (list -1.0 0.0)) Din D)
     (if (= n 2)
       (pr:daire-yerlestir 2 (nth 1 (pr:p "SPECS"))
                           (list (list (+ xc0 cw tp) 0.0) (list 0.0 1.0) (list 1.0 0.0)) Din D))
     T)
    (b
     ;; ----- DUZEN B
     (setq *pr-duzen* (list "B" (car b))
           xc0 (cadr b) dr (caddr b) ij (cadddr b) gerek (nth 4 b)
           yb (+ dr tp *pr-kat-holu* tp))
     (pr:mahal-ekle "HOL" "KAT HOLÜ" (list (list 0.0 (+ dr tp)) (list Lin (+ dr tp))
                                           (list Lin (+ dr tp *pr-kat-holu*)) (list 0.0 (+ dr tp *pr-kat-holu*)))
                    Lin *pr-kat-holu* 0 (list (+ xc0 (/ cw 2.0)) (+ dr tp (/ *pr-kat-holu* 2.0))))
     (pr:cekirdek xc0 yb Din tp)
     (setq nolar (pr:sayilar 1 n))
     ;; on sira (giris y=dr, v -y yonunde)
     (pr:segment-yerlestir (pr:alt nolar 0 (car ij)) (pr:alt gerek 0 (car ij))
                           0.0 Lin dr -1.0 dr)
     ;; arka sol ve arka sag (giris y=yb, v +y yonunde)
     (pr:segment-yerlestir (pr:alt nolar (car ij) (cadr ij)) (pr:alt gerek (car ij) (cadr ij))
                           0.0 (- xc0 tp) yb 1.0 dr)
     (pr:segment-yerlestir (pr:alt nolar (cadr ij) n) (pr:alt gerek (cadr ij) n)
                           (+ xc0 cw tp) Lin yb 1.0 dr)
     T)
    (T
     (pr:rapor "Seçilen alan bu daire sayısı için çok küçük; plan kurgulanamadı.")
     nil)))

(defun pr:sayilar (a b / res)
  (setq res nil)
  (while (<= a b) (setq res (cons a res) a (1+ a)))
  (reverse res))

;;; ---------------------------------------------------------------------------
;;;  7. CIZIM ONCESI SON ADIMLAR: SINIRLAR, ETIKETLER, UYGUNLUK RAPORU
;;; ---------------------------------------------------------------------------

(defun pr:mahal-kontrol (m / tip w d a s alan kisa ok)
  ;; -> (alan kisaKenar asgariAlan asgariKenar uygunMu) ya da nil (kontrolsuz)
  (setq tip (pr:tip (car m)) w (nth 3 m) d (nth 4 m))
  (cond
    ((= (car m) "HOL")
     (setq kisa (min w d))
     (list (pr:alan (caddr m)) kisa 0.0 *pr-kat-holu* (>= kisa (- *pr-kat-holu* 0.005))))
    ((= (car m) "ANTRE")
     (list (pr:alan (caddr m)) w 0.0 (nth 4 tip) (>= w (- (nth 4 tip) 0.005))))
    (tip
     (setq a (nth 3 tip) s (nth 4 tip)
           alan (* w d) kisa (min w d)
           ok (and (>= alan (- a 0.005)) (>= kisa (- s 0.005))))
     (list alan kisa a s ok))))

(defun pr:son-islem (/ k satir daireAlan no spec m tum uygunsuz mo yat sal)
  ;; sinirlar ve etiketler
  (foreach m *pr-mahaller*
    (pr:poligon-ciz (caddr m) "PR-DUVAR")
    (pr:push (list "TX" "PR-YAZI" (nth 6 m) *pr-yazi-boy* (cadr m) 0.22))
    (pr:push (list "TX" "PR-YAZI" (nth 6 m) (* 0.75 *pr-yazi-boy*)
                   (strcat (pr:fmt (pr:alan (caddr m))) " m²") 0.0))
    (if (and (nth 3 m) (nth 4 m) (not (member (car m) '("ANTRE" "HOL" "BOS"))))
      (pr:push (list "TX" "PR-YAZI" (nth 6 m) (* 0.65 *pr-yazi-boy*)
                     (strcat (pr:fmt (nth 3 m)) " x " (pr:fmt (nth 4 m))) -0.20))))
  ;; rapor
  (setq uygunsuz 0)
  (pr:rapor "PLAN ROBOTU - UYGUNLUK RAPORU")
  (pr:rapor "Planlı Alanlar İmar Yönetmeliği asgari ölçüleri (Md.29 piyesler, ortak merdiven/hol)")
  (pr:rapor (strcat "Kurgu: " (if (= (car *pr-duzen*) "A") "merdiven evi ortada, daireler iki yanda"
                                                       "orta kat holü, merdiven arka sırada")
                    "   Doluluk: %" (itoa (fix (+ 0.5 (* 100.0 (min 1000.0 (cadr *pr-duzen*))))))))
  (foreach d (reverse *pr-daireler*)
    (setq no (car d) spec (cadr d) daireAlan 0.0)
    (foreach m *pr-mahaller*
      (if (and (= (nth 5 m) no) (/= (car m) "BALKON"))
        (setq daireAlan (+ daireAlan (pr:alan (caddr m))))))
    (pr:rapor "")
    (pr:rapor (strcat "DAİRE " (itoa no) " (" (pr:spec-adi spec) ")  net alan: "
                      (pr:fmt daireAlan) " m²   (mutfak " (caddr d) " bantta)"))
    (foreach m (reverse *pr-mahaller*)
      (if (= (nth 5 m) no)
        (progn
          (setq k (pr:mahal-kontrol m))
          (if k
            (progn
              (if (not (nth 4 k)) (setq uygunsuz (1+ uygunsuz)))
              (pr:rapor (strcat "   " (cadr m) ": " (pr:fmt (car k)) " m²"
                                (if (> (caddr k) 0.0) (strcat " (asg. " (pr:fmt (caddr k)) ")") "")
                                ", dar kenar " (pr:fmt (cadr k)) " m (asg. " (pr:fmt (cadddr k)) ")"
                                "  -> " (if (nth 4 k) "UYGUN" "UYGUN DEĞİL"))))
            (if (= (car m) "BALKON")
              (pr:rapor (strcat "   BALKON: " (pr:fmt (pr:alan (caddr m))) " m² (emsal dışı değerlendirilir)")))))))
    (pr:rapor (strcat "   Daire içi koridor: " (pr:fmt *pr-koridor*) " m"))
    ;; Md.29 asgari piyes kosullari
    (setq yat (pr:spec-say spec "YATAK") sal (pr:spec-say spec "SALON"))
    (if (< (+ yat sal) 1)
      (progn (setq uygunsuz (1+ uygunsuz)) (pr:rapor "   UYGUN DEĞİL: dairede en az 1 oda olmalı.")))
    (if (< (pr:spec-say spec "MUTFAK") 1)
      (progn (setq uygunsuz (1+ uygunsuz)) (pr:rapor "   UYGUN DEĞİL: dairede mutfak / yemek pişirme yeri yok.")))
    (if (< (pr:spec-say spec "BANYO") 1)
      (progn (setq uygunsuz (1+ uygunsuz)) (pr:rapor "   UYGUN DEĞİL: dairede banyo / duş yeri yok.")))
    (if (< (pr:spec-say spec "WC") 1)
      (pr:rapor "   NOT: ayrı WC yok - hela banyo içinde çözülmelidir.")))
  ;; ortak alanlar
  (pr:rapor "")
  (pr:rapor "ORTAK ALANLAR")
  (foreach m (reverse *pr-mahaller*)
    (if (= (nth 5 m) 0)
      (progn
        (setq k (pr:mahal-kontrol m))
        (if (and k (not (nth 4 k))) (setq uygunsuz (1+ uygunsuz)))
        (pr:rapor (strcat "   " (cadr m) ": " (pr:fmt (pr:alan (caddr m))) " m²"
                          (if k (strcat ", dar kenar " (pr:fmt (cadr k)) " m (asg. " (pr:fmt (cadddr k))
                                        ")  -> " (if (nth 4 k) "UYGUN" "UYGUN DEĞİL"))
                              ""))))))
  (setq mo (pr:merdiven-olcu))
  (pr:rapor (strcat "   Merdiven: kol " (pr:fmt *pr-merdiven-kol*) " m, "
                    (itoa (* 2 (caddr mo))) " rıht x " (pr:fmt (* 100.0 (cadddr mo))) " cm, basamak "
                    (pr:fmt (* 100.0 *pr-basamak*)) " cm, 2a+b = "
                    (pr:fmt (* 100.0 (+ (* 2.0 (cadddr mo)) *pr-basamak*))) " cm"))
  (if (not (pr:p "ASANSOR"))
    (pr:rapor "   NOT: asansör çizilmedi - bina kat adedine göre asansör zorunluluğunu kontrol edin."))
  (foreach u (reverse *pr-uyarilar*) (pr:rapor (strcat "UYARI: " u)))
  (pr:rapor "")
  (pr:rapor (if (and (= uygunsuz 0) (= *pr-hata-sayisi* 0))
              "SONUÇ: Tüm mahaller yönetmelik asgari ölçülerini sağlıyor."
              (strcat "SONUÇ: " (itoa (+ uygunsuz *pr-hata-sayisi*))
                      " uygunsuzluk var - alanı büyütün veya mahal sayısını azaltın.")))
  (setq *pr-rapor* (reverse *pr-rapor*))
  (setq *pr-uygunsuz* (+ uygunsuz *pr-hata-sayisi*)))

;;; ---------------------------------------------------------------------------
;;;  8. AUTOCAD'E CIZIM
;;; ---------------------------------------------------------------------------

(defun pr:katmanlari-hazirla ()
  (foreach k *pr-katmanlar*
    (if (not (tblsearch "LAYER" (car k)))
      (entmake (list '(0 . "LAYER") '(100 . "AcDbSymbolTableRecord") '(100 . "AcDbLayerTableRecord")
                     (cons 2 (car k)) '(70 . 0) (cons 62 (cadr k)) '(6 . "Continuous")))))
  (if (not (tblsearch "STYLE" "PR_YAZI"))
    (entmake (list '(0 . "STYLE") '(100 . "AcDbSymbolTableRecord") '(100 . "AcDbTextStyleTableRecord")
                   '(2 . "PR_YAZI") '(70 . 0) '(40 . 0.0) '(41 . 1.0) '(50 . 0.0) '(71 . 0)
                   '(42 . 1.0) '(3 . "arial.ttf") '(4 . "")))))

;; cerceve: (O ex ey olcek z)  - O cizim birimi, olcek: birim/metre
(defun pr:w (cer p / td)
  (setq td (pr:p "TD"))
  (pr:3d (pr:v+ (car cer)
                (pr:v+ (pr:v* (cadr cer) (* (+ (car p) td) (cadddr cer)))
                       (pr:v* (caddr cer) (* (+ (cadr p) td) (cadddr cer)))))
         (nth 4 cer)))

(defun pr:3d (p z) (list (car p) (cadr p) z))

(defun pr:yazi (lay p h s rot)
  (entmake (list '(0 . "TEXT") (cons 8 lay) '(7 . "PR_YAZI") (cons 10 p) (cons 11 p)
                 (cons 40 h) (cons 1 s) (cons 50 rot) '(72 . 1) '(73 . 2))))

(defun pr:yazi-sol (lay p h s rot)
  (entmake (list '(0 . "TEXT") (cons 8 lay) '(7 . "PR_YAZI") (cons 10 p)
                 (cons 40 h) (cons 1 s) (cons 50 rot))))

(defun pr:ciz (cer / olc rot yukari p c p1 p2 b)
  (setq olc (cadddr cer)
        rot (angle '(0.0 0.0) (cadr cer))
        yukari (pr:dik (cadr cer)))
  (foreach e (reverse *pr-prims*)
    (cond
      ((= (car e) "LN")
       (entmake (list '(0 . "LINE") (cons 8 (cadr e))
                      (cons 10 (pr:w cer (caddr e))) (cons 11 (pr:w cer (cadddr e))))))
      ((= (car e) "PL")
       (entmake (append (list '(0 . "LWPOLYLINE") '(100 . "AcDbEntity") (cons 8 (cadr e))
                              '(100 . "AcDbPolyline") (cons 90 (length (caddr e)))
                              (cons 70 (if (cadddr e) 1 0)) (cons 38 (nth 4 cer)))
                        (mapcar '(lambda (q) (cons 10 (pr:butlast (pr:w cer q)))) (caddr e)))))
      ((= (car e) "ARC")
       (setq c (pr:w cer (caddr e)) p1 (pr:w cer (cadddr e)) p2 (pr:w cer (nth 4 e))
             b (if (> (pr:cross (pr:v- p1 c) (pr:v- p2 c)) 0.0) 0.41421356 -0.41421356))
       (entmake (list '(0 . "LWPOLYLINE") '(100 . "AcDbEntity") (cons 8 (cadr e))
                      '(100 . "AcDbPolyline") '(90 . 2) '(70 . 0) (cons 38 (nth 4 cer))
                      (cons 10 (pr:butlast p1)) (cons 42 b) (cons 10 (pr:butlast p2)))))
      ((= (car e) "TX")
       (setq p (pr:w cer (caddr e)))
       (setq p (pr:3d (pr:v+ p (pr:v* yukari (* (nth 5 e) olc))) (nth 4 cer)))
       (pr:yazi (cadr e) p (* (cadddr e) olc) (nth 4 e) rot))))
  ;; rapor tablosu: dis konturun sagina
  (if (pr:p "RAPOR")
    (progn
      (setq p (list (+ (pr:p "LX") 1.5 (- (pr:p "TD"))) (- (pr:p "LY") (pr:p "TD") 0.3)))
      (foreach s *pr-rapor*
        (pr:yazi-sol "PR-RAPOR" (pr:w cer p) (* 0.15 olc) s rot)
        (setq p (list (car p) (- (cadr p) 0.30)))))))

;;; ---------------------------------------------------------------------------
;;;  9. DIKDORTGEN SECIMI
;;; ---------------------------------------------------------------------------

(defun pr:lwpoly-noktalari (e / ed pts)
  (setq ed (entget e) pts nil)
  (foreach g ed
    (if (= (car g) 10) (setq pts (cons (list (cadr g) (caddr g)) pts))))
  (setq pts (reverse pts))
  (if (and (> (length pts) 4) (pr:ayni (car pts) (last pts)))
    (setq pts (pr:butlast pts)))
  pts)

;; -> (O ex ey Lx Ly) cizim biriminde ya da nil
(defun pr:dikdortgen-cerceve (pts / i a b c en ei ex ey o ly mrk)
  (if (/= (length pts) 4)
    nil
    (progn
      ;; dik acilar
      (setq i 0 en 0.0 ei 0)
      (foreach k '(0 1 2 3)
        (setq a (nth k pts) b (nth (rem (+ k 1) 4) pts) c (nth (rem (+ k 2) 4) pts))
        (if (> (abs (pr:dot (pr:birim (pr:v- b a)) (pr:birim (pr:v- c b)))) 0.001)
          (setq i 1))
        (if (> (pr:len (pr:v- b a)) en) (setq en (pr:len (pr:v- b a)) ei k)))
      (if (= i 1)
        nil
        (progn
          (setq o (nth ei pts) ex (pr:birim (pr:v- (nth (rem (1+ ei) 4) pts) o)))
          ;; yazilar okunakli olsun: ex soldan saga
          (if (or (< (car ex) -1e-9) (and (< (abs (car ex)) 1e-9) (< (cadr ex) 0.0)))
            (setq o (nth (rem (1+ ei) 4) pts) ex (pr:v* ex -1.0)))
          (setq mrk (pr:v* (pr:v+ (car pts) (caddr pts)) 0.5)
                ey (pr:dik ex))
          (if (< (pr:dot (pr:v- mrk o) ey) 0.0) (setq ey (pr:v* ey -1.0)))
          (setq ly 0.0)
          (foreach p pts (setq ly (max ly (pr:dot (pr:v- p o) ey))))
          (list o ex ey en ly))))))

;;; ---------------------------------------------------------------------------
;;;  10. FORM (DCL)
;;; ---------------------------------------------------------------------------

(defun pr:dcl-yolu (/ f yol)
  (setq yol (findfile "PlanRobotu.dcl"))
  (if (null yol)
    (progn
      ;; DCL bulunamazsa gecici klasore yazilir
      (setq yol (vl-filename-mktemp "planrobotu" nil ".dcl"))
      (setq f (vl-catch-all-apply 'open (list yol "w" "utf8")))
      (if (or (null f) (vl-catch-all-error-p f)) (setq f (open yol "w")))
      (foreach l *pr-dcl* (write-line l f))
      (close f)))
  yol)

;; DCL dosyasi BILEREK yalniz ASCII karakter icerir: AutoCAD .dcl dosyalarini
;; ANSI kod sayfasiyla okudugu icin Turkce harfler bozuluyordu. Tum Turkce
;; etiketler form acildiginda LISP'ten (Unicode) set_tile ile yazilir.
(setq *pr-dcl*
  '("planrobotu : dialog {"
    "  key = \"baslik\"; label = \"Plan Robotu\";"
    "  : text { key = \"t_baslik\"; width = 72; }"
    "  : row {"
    "    : boxed_column {"
    "      key = \"b_genel\"; label = \"Genel\";"
    "      : row { : text { key = \"t_daire_sayisi\"; width = 30; } : edit_box { key = \"daire_sayisi\"; edit_width = 6; fixed_width = true; } }"
    "      : row { : text { key = \"t_birim\"; width = 30; } : popup_list { key = \"birim\"; edit_width = 7; fixed_width = true; } }"
    "      : row { : text { key = \"t_dis_duvar\"; width = 30; } : edit_box { key = \"dis_duvar\"; edit_width = 6; fixed_width = true; } }"
    "      : row { : text { key = \"t_ic_duvar\"; width = 30; } : edit_box { key = \"ic_duvar\"; edit_width = 6; fixed_width = true; } }"
    "      : row { : text { key = \"t_ayirici_duvar\"; width = 30; } : edit_box { key = \"ayirici_duvar\"; edit_width = 6; fixed_width = true; } }"
    "      : row { : text { key = \"t_kat_yuk\"; width = 30; } : edit_box { key = \"kat_yuk\"; edit_width = 6; fixed_width = true; } }"
    "      : row { : toggle { key = \"asansor\"; width = 3; fixed_width = true; } : text { key = \"t_asansor\"; width = 33; } }"
    "      : row { : toggle { key = \"rapor\"; width = 3; fixed_width = true; } : text { key = \"t_rapor\"; width = 33; } }"
    "      : text { key = \"alan\"; width = 38; }"
    "    }"
    "    : boxed_column {"
    "      key = \"b_mahal\"; label = \"Daire\";"
    "      : row { : text { key = \"t_daire_sec\"; width = 18; } : popup_list { key = \"daire_sec\"; edit_width = 16; fixed_width = true; } }"
    "      : row { : text { key = \"t_sablon\"; width = 18; } : popup_list { key = \"sablon\"; edit_width = 16; fixed_width = true; } }"
    "      : row { : text { key = \"t_SALON\"; width = 26; } : edit_box { key = \"m_SALON\"; edit_width = 4; fixed_width = true; } }"
    "      : row { : text { key = \"t_YATAK\"; width = 26; } : edit_box { key = \"m_YATAK\"; edit_width = 4; fixed_width = true; } }"
    "      : row { : text { key = \"t_MUTFAK\"; width = 26; } : edit_box { key = \"m_MUTFAK\"; edit_width = 4; fixed_width = true; } }"
    "      : row { : text { key = \"t_BANYO\"; width = 26; } : edit_box { key = \"m_BANYO\"; edit_width = 4; fixed_width = true; } }"
    "      : row { : text { key = \"t_WC\"; width = 26; } : edit_box { key = \"m_WC\"; edit_width = 4; fixed_width = true; } }"
    "      : row { : text { key = \"t_KILER\"; width = 26; } : edit_box { key = \"m_KILER\"; edit_width = 4; fixed_width = true; } }"
    "      : row { : text { key = \"t_BALKON\"; width = 26; } : edit_box { key = \"m_BALKON\"; edit_width = 4; fixed_width = true; } }"
    "      : button { key = \"tumune\"; label = \"Tum dairelere kopyala\"; }"
    "    }"
    "  }"
    "  : text { key = \"bilgi\"; width = 72; }"
    "  ok_cancel;"
    "}"))

;; Form etiketleri (Unicode, calisma aninda yazilir)
(setq *pr-form-yazilar*
  '(("t_baslik" "Planlı Alanlar İmar Yönetmeliği asgari ölçülerine göre kat planı")
    ("t_daire_sayisi" "Daire sayısı:")
    ("t_birim" "Çizim birimi:")
    ("t_dis_duvar" "Dış duvar kalınlığı (cm):")
    ("t_ic_duvar" "İç duvar kalınlığı (cm):")
    ("t_ayirici_duvar" "Daire / hol ayırıcı duvar (cm):")
    ("t_kat_yuk" "Kat yüksekliği (cm):")
    ("t_asansor" "Asansör kuyusu çiz")
    ("t_rapor" "Uygunluk raporunu çizime yaz")
    ("t_daire_sec" "Düzenlenen daire:")
    ("t_sablon" "Hazır şablon:")
    ("t_SALON" "Salon / Oturma odası:")
    ("t_YATAK" "Yatak odası:")
    ("t_MUTFAK" "Mutfak:")
    ("t_BANYO" "Banyo:")
    ("t_WC" "WC (hela):")
    ("t_KILER" "Kiler:")
    ("t_BALKON" "Balkon:")
    ("tumune" "Bu daireyi tüm dairelere kopyala")
    ("baslik" "Plan Robotu 1.3 - Kat Planı")
    ("b_genel" "Genel Ayarlar")
    ("b_mahal" "Daire Mahalleri")
    ("accept" "Tamam")
    ("cancel" "İptal")))

;; Secilen dikdortgenin olculerini forma yazar (birim hatasini gormek icin)
(defun pr:f-alan-goster (/ olc lx ly)
  (setq olc (nth (atoi (get_tile "birim")) '(100.0 1.0 1000.0))
        lx (/ (car *pr-secim-boy*) olc)
        ly (/ (cadr *pr-secim-boy*) olc))
  (set_tile "alan" (strcat "Seçilen alan: " (pr:fmt lx) " x " (pr:fmt ly) " m"))
  (if (or (< lx 6.0) (> lx 400.0))
    (set_tile "bilgi" "Uyarı: alan ölçüsü bina için olağan dışı - çizim birimini kontrol edin.")
    (set_tile "bilgi" "")))

;; Dikdortgen boyutundan cizim birimini tahmin eder (0=cm 1=m 2=mm)
(defun pr:birim-tahmin (uzun)
  (cond ((<= uzun 400.0) "1")
        ((<= uzun 40000.0) "0")
        (T "2")))

;; Oturum boyunca hatirlanan son form degerleri
(if (null *pr-form*)
  (setq *pr-form*
    (list (list "daire_sayisi" "2") (list "birim" "0") (list "dis_duvar" "25")
          (list "ic_duvar" "10") (list "ayirici_duvar" "20") (list "kat_yuk" "300")
          (list "asansor" "1") (list "rapor" "1"))))
(if (null *pr-form-specs*)
  (setq *pr-form-specs* (list '(1 2 1 1 1 0 1) '(1 2 1 1 1 0 1))))

(defun pr:f (k) (cadr (assoc k *pr-form*)))
(defun pr:f-set (k v)
  (setq *pr-form* (subst (list k v) (assoc k *pr-form*) *pr-form*)))

(defun pr:tamsayi (s / n)
  (setq n (if (and s (/= s "")) (atoi s) 0))
  (if (< n 0) 0 n))

(defun pr:f-mahal-oku ()
  (mapcar '(lambda (k) (pr:tamsayi (get_tile (strcat "m_" k)))) *pr-form-kodlar*))

(defun pr:f-mahal-yaz (spec)
  (mapcar '(lambda (k n) (set_tile (strcat "m_" k) (itoa n))) *pr-form-kodlar* spec))

(defun pr:f-liste-adlari (/ i)
  (setq i 0)
  (start_list "daire_sec")
  (foreach s *pr-form-specs*
    (setq i (1+ i))
    (add_list (strcat "Daire " (itoa i) "  (" (pr:spec-adi s) ")")))
  (end_list)
  (set_tile "daire_sec" (itoa *pr-f-cur*)))

(defun pr:f-mevcut-kaydet ()
  (setq *pr-form-specs*
    (pr:liste-degistir *pr-form-specs* *pr-f-cur* (pr:f-mahal-oku))))

(defun pr:liste-degistir (l i v / res k)
  (setq res nil k 0)
  (foreach x l
    (setq res (cons (if (= k i) v x) res) k (1+ k)))
  (reverse res))

(defun pr:f-daire-sayisi (val / n)
  (setq n (pr:tamsayi val))
  (if (or (< n 1) (> n 30))
    (set_tile "bilgi" "Daire sayısı 1 ile 30 arasında olmalıdır.")
    (progn
      (pr:f-mevcut-kaydet)
      (while (< (length *pr-form-specs*) n)
        (setq *pr-form-specs* (append *pr-form-specs* (list (last *pr-form-specs*)))))
      (if (> (length *pr-form-specs*) n)
        (setq *pr-form-specs* (pr:alt *pr-form-specs* 0 n)))
      (if (>= *pr-f-cur* n) (setq *pr-f-cur* (1- n)))
      (pr:f-liste-adlari)
      (pr:f-mahal-yaz (nth *pr-f-cur* *pr-form-specs*))
      (set_tile "bilgi" ""))))

(defun pr:f-daire-sec (val)
  (pr:f-mevcut-kaydet)
  (setq *pr-f-cur* (atoi val))
  (pr:f-mahal-yaz (nth *pr-f-cur* *pr-form-specs*)))

(defun pr:f-sablon (val / i)
  (setq i (atoi val))
  (if (> i 0)
    (progn
      (pr:f-mahal-yaz (cadr (nth (1- i) *pr-sablonlar*)))
      (pr:f-mevcut-kaydet)
      (pr:f-liste-adlari)
      (set_tile "sablon" "0"))))

(defun pr:f-mahal-degisti ()
  (pr:f-mevcut-kaydet)
  (pr:f-liste-adlari))

(defun pr:f-tumune (/ s)
  (pr:f-mevcut-kaydet)
  (setq s (nth *pr-f-cur* *pr-form-specs*))
  (setq *pr-form-specs* (mapcar '(lambda (x) s) *pr-form-specs*))
  (pr:f-liste-adlari)
  (set_tile "bilgi" "Mahal listesi tüm dairelere kopyalandı."))

(defun pr:sayi-mi (s) (and s (distof s 2) (> (distof s 2) 0.0)))

(defun pr:f-tamam (/ hata i)
  (pr:f-daire-sayisi (get_tile "daire_sayisi"))
  (pr:f-mevcut-kaydet)
  (foreach k '("dis_duvar" "ic_duvar" "ayirici_duvar" "kat_yuk")
    (if (and (null hata) (not (pr:sayi-mi (get_tile k))))
      (setq hata (strcat "Geçersiz değer: " k))))
  (if (and (null hata) (/= (length *pr-form-specs*) (pr:tamsayi (get_tile "daire_sayisi"))))
    (setq hata "Daire sayısını değiştirdikten sonra Enter'a basın veya başka alana geçin."))
  (setq i 0)
  (foreach s *pr-form-specs*
    (setq i (1+ i))
    (cond
      (hata nil)
      ((< (+ (pr:spec-say s "SALON") (pr:spec-say s "YATAK")) 1)
       (setq hata (strcat "Daire " (itoa i) ": en az 1 oda (salon veya yatak odası) gerekir (PAİY Md.29).")))
      ((< (pr:spec-say s "MUTFAK") 1)
       (setq hata (strcat "Daire " (itoa i) ": en az 1 mutfak gerekir (PAİY Md.29).")))
      ((< (pr:spec-say s "BANYO") 1)
       (setq hata (strcat "Daire " (itoa i) ": en az 1 banyo gerekir (PAİY Md.29).")))))
  (if hata
    (set_tile "bilgi" hata)
    (progn
      (foreach k '("daire_sayisi" "birim" "dis_duvar" "ic_duvar" "ayirici_duvar" "kat_yuk" "asansor" "rapor")
        (pr:f-set k (get_tile k)))
      (done_dialog 1))))

(defun pr:form-ac (/ id sonuc)
  (setq id (load_dialog (pr:dcl-yolu)))
  (if (or (< id 0) (not (new_dialog "planrobotu" id)))
    (progn (alert "Plan Robotu formu yüklenemedi (PlanRobotu.dcl).") nil)
    (progn
      (setq *pr-f-cur* 0)
      (while (< (length *pr-form-specs*) (pr:tamsayi (pr:f "daire_sayisi")))
        (setq *pr-form-specs* (append *pr-form-specs* (list (last *pr-form-specs*)))))
      (foreach y *pr-form-yazilar* (set_tile (car y) (cadr y)))
      (start_list "birim") (mapcar 'add_list '("cm" "m" "mm")) (end_list)
      (start_list "sablon")
      (add_list "Seçiniz...")
      (foreach s *pr-sablonlar* (add_list (car s)))
      (end_list)
      (pr:f-set "birim" (pr:birim-tahmin (car *pr-secim-boy*)))
      (foreach k '("daire_sayisi" "birim" "dis_duvar" "ic_duvar" "ayirici_duvar" "kat_yuk" "asansor" "rapor")
        (set_tile k (pr:f k)))
      (set_tile "sablon" "0")
      (pr:f-liste-adlari)
      (pr:f-mahal-yaz (nth 0 *pr-form-specs*))
      (pr:f-alan-goster)
      (action_tile "daire_sayisi" "(pr:f-daire-sayisi $value)")
      (action_tile "birim" "(pr:f-alan-goster)")
      (action_tile "daire_sec" "(pr:f-daire-sec $value)")
      (action_tile "sablon" "(pr:f-sablon $value)")
      (foreach k *pr-form-kodlar* (action_tile (strcat "m_" k) "(pr:f-mahal-degisti)"))
      (action_tile "tumune" "(pr:f-tumune)")
      (action_tile "accept" "(pr:f-tamam-guvenli)")
      (action_tile "cancel" "(done_dialog 0)")
      (setq sonuc (start_dialog))
      (unload_dialog id)
      (= sonuc 1))))

;; OK dugmesi: olasi bir hata formu sessizce kilitlemesin, mesaj gostersin
(defun pr:f-tamam-guvenli (/ r)
  (setq r (vl-catch-all-apply 'pr:f-tamam nil))
  (if (vl-catch-all-error-p r)
    (set_tile "bilgi" (strcat "Hata: " (vl-catch-all-error-message r)))))

;; Form degerlerinden hesap parametrelerini (metre) kurar
(defun pr:parametreler (Lx Ly / cm)
  (setq cm 0.01)
  (setq *pr-par*
    (list (list "N" (length *pr-form-specs*))
          (list "SPECS" *pr-form-specs*)
          (list "TD" (* cm (distof (pr:f "dis_duvar") 2)))
          (list "TI" (* cm (distof (pr:f "ic_duvar") 2)))
          (list "TP" (* cm (distof (pr:f "ayirici_duvar") 2)))
          (list "H"  (* cm (distof (pr:f "kat_yuk") 2)))
          (list "ASANSOR" (= (pr:f "asansor") "1"))
          (list "RAPOR" (= (pr:f "rapor") "1"))
          (list "LX" Lx) (list "LY" Ly))))

(defun pr:olcek ()
  (nth (atoi (pr:f "birim")) '(100.0 1.0 1000.0)))

;;; ---------------------------------------------------------------------------
;;;  11. KOMUT
;;; ---------------------------------------------------------------------------

(defun pr:uygula (sel cer / olc z)
  (setq olc (pr:olcek)
        z (cond ((cdr (assoc 38 (entget (car sel))))) (0.0)))
  (pr:parametreler (/ (cadddr cer) olc) (/ (nth 4 cer) olc))
  (if (pr:plan-uret (pr:p "LX") (pr:p "LY"))
    (progn
      (pr:son-islem)
      (pr:katmanlari-hazirla)
      (pr:ciz (list (car cer) (cadr cer) (caddr cer) olc z))
      T)
    (progn
      (setq *pr-rapor* (reverse *pr-rapor*))
      nil)))

(defun pr:metin-birlestir (l / s)
  (setq s "")
  (foreach x l (setq s (strcat s x "\n")))
  s)

(defun c:PLANROBOTU (/ *error* doc sel pts cer r eskiOsm)
  (setq doc (vla-get-ActiveDocument (vlax-get-acad-object)))
  (defun *error* (msg)
    (if eskiOsm (setvar "OSMODE" eskiOsm))
    (vla-EndUndoMark doc)
    (if (not (wcmatch (strcase msg T) "*break*,*cancel*,*exit*,*iptal*"))
      (alert (strcat "Plan Robotu hatası: " msg)))
    (princ))
  (setq sel (entsel "\nKat planı çizilecek dikdörtgeni (kapalı polyline) seçin: "))
  (cond
    ((null sel) (princ "\nSeçim yapılmadı."))
    ((/= (cdr (assoc 0 (entget (car sel)))) "LWPOLYLINE")
     (alert "Seçilen nesne LWPOLYLINE değil.\nDikdörtgeni RECTANG veya kapalı PLINE ile çizin."))
    ((null (setq cer (pr:dikdortgen-cerceve (setq pts (pr:lwpoly-noktalari (car sel))))))
     (alert "Seçilen polyline 4 köşeli ve dik açılı bir dikdörtgen olmalıdır."))
    (T
     (setq *pr-secim-boy* (list (cadddr cer) (nth 4 cer)))
     (if (pr:form-ac)
       (progn
         (vla-StartUndoMark doc)
         (setq eskiOsm (getvar "OSMODE"))
         (setvar "OSMODE" 0)
         (setq r (vl-catch-all-apply 'pr:uygula (list sel cer)))
         (setvar "OSMODE" eskiOsm)
         (setq eskiOsm nil)
         (vla-EndUndoMark doc)
         (cond
           ((vl-catch-all-error-p r)
            (alert (strcat "Plan Robotu çizim sırasında hata verdi:\n"
                           (vl-catch-all-error-message r))))
           ((null r)
            (foreach s *pr-rapor* (princ (strcat "\n" s)))
            (alert (strcat "Plan çizilemedi:\n\n" (pr:metin-birlestir *pr-rapor*)
                           "\nSeçilen alan: " (pr:fmt (pr:p "LX")) " x " (pr:fmt (pr:p "LY"))
                           " m (çizim birimi doğru mu?)")))
           (T
            (foreach s *pr-rapor* (princ (strcat "\n" s)))
            (princ "\n")
            (alert (strcat "Plan Robotu tamamlandı.\n\n"
                           "Alan: " (pr:fmt (pr:p "LX")) " x " (pr:fmt (pr:p "LY")) " m, "
                           (itoa (pr:p "N")) " daire\n"
                           (if (= *pr-uygunsuz* 0) "Tüm kontroller UYGUN."
                               (strcat (itoa *pr-uygunsuz*) " uygunsuzluk raporlandı."))
                           "\n\nAyrıntılı rapor komut satırında (F2)"
                           (if (pr:p "RAPOR") " ve planın sağında." ".")))))))))
  (princ))

(defun c:PROBOT () (c:PLANROBOTU))

(princ (strcat "\nPlan Robotu " *pr-surum* " yüklendi. Komut: PLANROBOTU (kısa: PROBOT)"))
(princ)
