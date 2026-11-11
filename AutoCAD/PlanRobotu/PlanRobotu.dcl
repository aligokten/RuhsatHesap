// Plan Robotu formu - araclar/dcl_uret.py ile uretilir, elle duzenlemeyin.
// Kodlama: Windows-1254 (AutoCAD .dcl dosyalarini ANSI okur).
planrobotu : dialog {
  key = "baslik"; label = "Plan Robotu 1.3 - Kat Planý";
  : text { key = "t_baslik"; label = "Planlý Alanlar Ýmar Yönetmeliði asgari ölçülerine göre kat planý"; width = 72; }
  : row {
    : boxed_column {
      key = "b_genel"; label = "Genel Ayarlar";
      : row { : text { key = "t_daire_sayisi"; label = "Daire sayýsý:"; width = 30; } : edit_box { key = "daire_sayisi"; edit_width = 6; fixed_width = true; } }
      : row { : text { key = "t_birim"; label = "Çizim birimi:"; width = 30; } : popup_list { key = "birim"; edit_width = 7; fixed_width = true; } }
      : row { : text { key = "t_dis_duvar"; label = "Dýþ duvar kalýnlýðý (cm):"; width = 30; } : edit_box { key = "dis_duvar"; edit_width = 6; fixed_width = true; } }
      : row { : text { key = "t_ic_duvar"; label = "Ýç duvar kalýnlýðý (cm):"; width = 30; } : edit_box { key = "ic_duvar"; edit_width = 6; fixed_width = true; } }
      : row { : text { key = "t_ayirici_duvar"; label = "Daire / hol ayýrýcý duvar (cm):"; width = 30; } : edit_box { key = "ayirici_duvar"; edit_width = 6; fixed_width = true; } }
      : row { : text { key = "t_kat_yuk"; label = "Kat yüksekliði (cm):"; width = 30; } : edit_box { key = "kat_yuk"; edit_width = 6; fixed_width = true; } }
      : row { : toggle { key = "asansor"; width = 3; fixed_width = true; } : text { key = "t_asansor"; label = "Asansör kuyusu çiz"; width = 33; } }
      : row { : toggle { key = "rapor"; width = 3; fixed_width = true; } : text { key = "t_rapor"; label = "Uygunluk raporunu çizime yaz"; width = 33; } }
      : text { key = "alan"; width = 38; }
    }
    : boxed_column {
      key = "b_mahal"; label = "Daire Mahalleri";
      : row { : text { key = "t_daire_sec"; label = "Düzenlenen daire:"; width = 18; } : popup_list { key = "daire_sec"; edit_width = 16; fixed_width = true; } }
      : row { : text { key = "t_sablon"; label = "Hazýr þablon:"; width = 18; } : popup_list { key = "sablon"; edit_width = 16; fixed_width = true; } }
      : row { : text { key = "t_SALON"; label = "Salon / Oturma odasý:"; width = 26; } : edit_box { key = "m_SALON"; edit_width = 4; fixed_width = true; } }
      : row { : text { key = "t_YATAK"; label = "Yatak odasý:"; width = 26; } : edit_box { key = "m_YATAK"; edit_width = 4; fixed_width = true; } }
      : row { : text { key = "t_MUTFAK"; label = "Mutfak:"; width = 26; } : edit_box { key = "m_MUTFAK"; edit_width = 4; fixed_width = true; } }
      : row { : text { key = "t_BANYO"; label = "Banyo:"; width = 26; } : edit_box { key = "m_BANYO"; edit_width = 4; fixed_width = true; } }
      : row { : text { key = "t_WC"; label = "WC (hela):"; width = 26; } : edit_box { key = "m_WC"; edit_width = 4; fixed_width = true; } }
      : row { : text { key = "t_KILER"; label = "Kiler:"; width = 26; } : edit_box { key = "m_KILER"; edit_width = 4; fixed_width = true; } }
      : row { : text { key = "t_BALKON"; label = "Balkon:"; width = 26; } : edit_box { key = "m_BALKON"; edit_width = 4; fixed_width = true; } }
      : button { key = "tumune"; label = "Bu daireyi tüm dairelere kopyala"; }
    }
  }
  : text { key = "bilgi"; width = 72; }
  ok_cancel;
}
