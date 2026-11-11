// Plan Robotu formu - PlanRobotu.lsp icindeki *pr-dcl* ile ayni tutulmalidir.
planrobotu : dialog {
  label = "Plan Robotu - Kat Planı (Planlı Alanlar İmar Yönetmeliği)";
  : row {
    : boxed_column {
      label = "Genel Ayarlar";
      : edit_box { key = "daire_sayisi"; label = "Daire sayısı:"; edit_width = 6; }
      : popup_list { key = "birim"; label = "Çizim birimi:"; edit_width = 8; }
      : edit_box { key = "dis_duvar"; label = "Dış duvar kalınlığı (cm):"; edit_width = 6; }
      : edit_box { key = "ic_duvar"; label = "İç duvar kalınlığı (cm):"; edit_width = 6; }
      : edit_box { key = "ayirici_duvar"; label = "Daire / hol ayırıcı duvar (cm):"; edit_width = 6; }
      : edit_box { key = "kat_yuk"; label = "Kat yüksekliği (cm):"; edit_width = 6; }
      : toggle { key = "asansor"; label = "Asansör kuyusu çiz"; }
      : toggle { key = "rapor"; label = "Uygunluk raporunu çizime yaz"; }
    }
    : boxed_column {
      label = "Daire Mahalleri";
      : popup_list { key = "daire_sec"; label = "Düzenlenen daire:"; edit_width = 18; }
      : popup_list { key = "sablon"; label = "Hazır şablon:"; edit_width = 18; }
      : edit_box { key = "m_SALON"; label = "Salon / Oturma odası:"; edit_width = 4; }
      : edit_box { key = "m_YATAK"; label = "Yatak odası:"; edit_width = 4; }
      : edit_box { key = "m_MUTFAK"; label = "Mutfak:"; edit_width = 4; }
      : edit_box { key = "m_BANYO"; label = "Banyo:"; edit_width = 4; }
      : edit_box { key = "m_WC"; label = "WC (hela):"; edit_width = 4; }
      : edit_box { key = "m_KILER"; label = "Kiler:"; edit_width = 4; }
      : edit_box { key = "m_BALKON"; label = "Balkon:"; edit_width = 4; }
      : button { key = "tumune"; label = "Bu daireyi tüm dairelere kopyala"; }
    }
  }
  : text { key = "bilgi"; width = 70; }
  ok_cancel;
}
