// Plan Robotu formu - PlanRobotu.lsp icindeki *pr-dcl* ile ayni tutulmalidir.
// Yalniz ASCII: Turkce etiketler calisma aninda LISP'ten yazilir.
planrobotu : dialog {
  key = "baslik"; label = "Plan Robotu";
  : text { key = "t_baslik"; width = 72; }
  : row {
    : boxed_column {
      key = "b_genel"; label = "Genel";
      : row { : text { key = "t_daire_sayisi"; width = 30; } : edit_box { key = "daire_sayisi"; edit_width = 6; fixed_width = true; } }
      : row { : text { key = "t_birim"; width = 30; } : popup_list { key = "birim"; edit_width = 7; fixed_width = true; } }
      : row { : text { key = "t_dis_duvar"; width = 30; } : edit_box { key = "dis_duvar"; edit_width = 6; fixed_width = true; } }
      : row { : text { key = "t_ic_duvar"; width = 30; } : edit_box { key = "ic_duvar"; edit_width = 6; fixed_width = true; } }
      : row { : text { key = "t_ayirici_duvar"; width = 30; } : edit_box { key = "ayirici_duvar"; edit_width = 6; fixed_width = true; } }
      : row { : text { key = "t_kat_yuk"; width = 30; } : edit_box { key = "kat_yuk"; edit_width = 6; fixed_width = true; } }
      : row { : toggle { key = "asansor"; width = 3; fixed_width = true; } : text { key = "t_asansor"; width = 33; } }
      : row { : toggle { key = "rapor"; width = 3; fixed_width = true; } : text { key = "t_rapor"; width = 33; } }
      : text { key = "alan"; width = 38; }
    }
    : boxed_column {
      key = "b_mahal"; label = "Daire";
      : row { : text { key = "t_daire_sec"; width = 18; } : popup_list { key = "daire_sec"; edit_width = 16; fixed_width = true; } }
      : row { : text { key = "t_sablon"; width = 18; } : popup_list { key = "sablon"; edit_width = 16; fixed_width = true; } }
      : row { : text { key = "t_SALON"; width = 26; } : edit_box { key = "m_SALON"; edit_width = 4; fixed_width = true; } }
      : row { : text { key = "t_YATAK"; width = 26; } : edit_box { key = "m_YATAK"; edit_width = 4; fixed_width = true; } }
      : row { : text { key = "t_MUTFAK"; width = 26; } : edit_box { key = "m_MUTFAK"; edit_width = 4; fixed_width = true; } }
      : row { : text { key = "t_BANYO"; width = 26; } : edit_box { key = "m_BANYO"; edit_width = 4; fixed_width = true; } }
      : row { : text { key = "t_WC"; width = 26; } : edit_box { key = "m_WC"; edit_width = 4; fixed_width = true; } }
      : row { : text { key = "t_KILER"; width = 26; } : edit_box { key = "m_KILER"; edit_width = 4; fixed_width = true; } }
      : row { : text { key = "t_BALKON"; width = 26; } : edit_box { key = "m_BALKON"; edit_width = 4; fixed_width = true; } }
      : button { key = "tumune"; label = "Tum dairelere kopyala"; }
    }
  }
  : text { key = "bilgi"; width = 72; }
  ok_cancel;
}
