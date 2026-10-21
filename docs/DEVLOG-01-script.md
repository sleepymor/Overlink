# DEVLOG #01 — "Kami Bikin Overlink Pakai Tim Agent AI"

**Versi bahasa Indonesia.** Referensi terjemahan: `DEVLOG-01-script-en.md`
**Target durasi:** 3–5 menit (terukur 674 kata narasi @ 150 kata/menit ≈ 4:30)
**Voice:** orang pertama, tenang, jujur, sedikit kering
**Format rekaman:** screen capture only — tidak ada facecam. Semua B-roll adalah tangkapan layar (Unity editor, terminal, file, browser). Tidak ada shot wajah atau kamera fisik.
**Catatan untuk editor:** semua klaim di bawah benar adanya per 2026-10-18. Jangan tambahkan klaim "tes sudah lulus" atau tanggal rilis — keduanya belum ada. Baris bertanda [POTONG] boleh dibuang untuk mengejar 3:00 tanpa merusak alur.

---

## 0:00–0:15 — Pembuka dingin

**Narasi:**
"Setiap promosi game kartu punya kalimat yang sama persis. 'Bayangin Hearthstone, tapi...' — terus ada yang ngomongin soal wilayah. Nah, ini versi gue."

**Screen capture:** layar hitam (atau wallpaper gelap), overlay kartu masuk lewat video editor, judul **OVERLINK** muncul sebagai teks animasi.

---

## 0:15–0:40 — Hook

**Narasi:**
"Overlink itu game kartu taktis. Lo dan lawan masing-masing pegang satu Core, dan di antara kalian ada grid Node. Kartu bukan cuma tuker-tukeran damage — posisi kartu lo *berdiri* yang nentuin siapa yang dibayar. Kuasai sebuah Node, lo dapat energi tambahan tiap turn plus buff permanen buat seluruh barisan lo. Kehilangan Node-nya, buff-nya mati seketika. Menang di posisi papan, dan matematikanya mulai main buat lo."

**Screen capture:** editor Unity atau mockup statis — sorot empat Node di Gameplay scene; animasi unit masuk ke slot Node Active; counter NP naik di HUD.

---

## 0:40–1:35 — Aturannya, dalam enam puluh detik

**Narasi:**
"Match jalan di enam phase: Upkeep, Draw, Main, Combat, End, Cleanup. Di Upkeep lo dapat satu energi — plus satu lagi buat setiap Node yang lo kuasai. Di Main lo deploy; di Combat lo serang dan blok.

Tiga cara menang. Hancurkan Core lawan. Dominasi papan — kuasai tiga dari empat Node di awal turn lo. Atau gerus dia sampai nggak bisa draw lagi.

Aturan deck-nya dijaga ketat: 30 sampai 40 kartu, maksimal tiga copy, satu faction di MVP. Sekarang itu FSTI — si pemegang jaringan. Kartu support permanen hidup di slot Bench, dan tiap player cuma punya satu slot Protocol, jadi Protocol baru menggantikan yang lama, bukan numpuk. Satu Protocol, satu keputusan besar.

[POTONG] Effect itu data, bukan kode. Setiap kartu adalah daftar terurut effect descriptor bertipe, dan semuanya divalidasi di tiga titik: waktu lo edit, di manager tool, dan lagi pas runtime startup. Kartu yang mustahil jalan nggak akan pernah sampai ke build."

**Screen capture:** rekaman HUD phase tracker maju di editor; teks overlay di video editor melabeli Core / Nodes / Bench / Protocol; ikon win-condition berkedip.

---

## 1:35–2:50 — Bagian yang beneran devlog: gimana ini dibikin

**Narasi:**
"Sekarang — bagian yang bikin ini devlog, bukan trailer. Gue bikin ini pakai tim kecil, dan 'tim' di sini termasuk agent AI. Ada koordinator, ada eksekutor yang nulis kodenya, dan ada reviewer yang tugas utamanya nggak setuju sama gue sebelum kodenya nyampe ke build.

Dan jujur? Reviewer-nya kali ini beneran berguna.

Kita punya game design document — lebih dari seratus halaman, pakai versi, dikunci pakai checksum biar nggak ada yang bisa ngedit diam-diam. Agent-nya bangun kode berdasarkan plan yang diturunkan dari dokumen itu, dan review statis nemu dua tempat di mana plan dan dokumen udah diam-diam berbeda.

Satu: plan-nya nyebut papan punya tiga Node. Dokumennya bilang empat. Tiga lawan empat itu bukan typo — itu ngubah seluruh win condition, karena dominasi artinya tiga dari empat, bukan dua dari tiga.

Dua — yang ini lebih parah. Plan-nya nyebut Core durability dua puluh. Dokumennya bilang tiga. Dua puluh itu perang atrisi. Tiga itu setiap pukulan berarti dan blocking itu hidup-mati. Game yang beda total. Dan nggak ada yang sadar karena plan-nya ditulis sebelum revisi terakhir dokumen itu masuk.

Dua-duanya udah dibenerin di kode minggu ini. Jumlah Node, ambang dominasi, durability, enam phase turn lengkap dengan damage transfer dan buff Node. Dan reviewer-nya masih jalan — dia gagalin cek terakhir kita di tiga hal yang bakal gue bahas di log berikutnya."

**Screen capture:** terminal PowerShell scroll; output `git status`; PDF GDD dibuka di browser berdampingan dengan file plan di VS Code; tampilan side-by-side dua diff di editor; teks overlay verdict FAILED merah.

---

## 2:50–3:40 — Kondisi sekarang apa adanya

**Narasi:**
"Jadi gini kondisi proyeknya jujur. Rules engine dan presentasi papan itu udah ada dalam bentuk kode. Ada scene yang bisa dimainin: ada grid-nya, ada hand UI-nya, drag-to-deploy, ada lawan AI deterministik yang harus lolos cek legalitas yang sama kayak lo — dia nggak bisa curang, artinya kalau dia menang, dia menang beneran.

Ada pilot set dua puluh kartu, dan ada card manager window buat bikin serta ngecek validitasnya.

[POTONG] Yang belum kelar: JSON yang harusnya dibaca runtime masih kosong, scene-nya belum masuk build list, dan combat targeting punya celah yang ketemu reviewer — serangannya bisa ngambil target dari battlefield yang salah. Dan semua ini belum dikompilasi atau dites di depan kamera, karena Unity editor megang lock file sampai penulisannya selesai.

Itu kondisi proyeknya beneran, bukan versi polesannya. Gue lebih milih nunjukin ini daripada highlight reel."

**Screen capture:** Unity editor dengan Gameplay scene terbuka; wireframe papan terlihat di Scene view dengan label slot; panel HUD log; tangan kartu di Game view; buka `cards.json` di editor teks sebentar — isinya kosong, biarkan terlihat.

---

## 3:40–4:40 — Log berikutnya + penutup

**Narasi:**
"Berikutnya: benerin scoping targeting, generate JSON kartu beneran dan bikin gamenya beneran baca itu, masukin scene ke build list, terus nyalain Unity lagi buat sesi compile-and-test beneran di depan kamera. Habis itu, dua puluh kartu di-tune jadi deck yang mainnya kayak yang dibilang design doc, dan satu match yang bisa selamat dari reviewer.

Kalau lo mau ngelihat pipeline AI-assisted yang entah berhasil spektakuler entah gagal di depan umum — subscribe, dua-duanya bakal kejadian di sini.

Overlink. Slafurry Studios. Makasih udah nonton."

**Screen capture:** file roadmap atau task list terbuka; kartu logo Overlink sebagai gambar penutup di layar; sebut itch.io cuma kalau halamannya beneran udah ada, kalau belum lewati saja.

---

## Alternatif pembuka dingin

1. **Hook angka:** "Minggu ini ada dua angka yang salah di plan build kita. Salah satunya ngubah game tiga pukulan jadi game dua puluh pukulan. Gue kasih lihat cara kita nangkepnya."
2. **Hook reviewer:** "Gue punya satu rekan kerja yang tugasnya bilang gue salah. Hari ini dia ngelakuin itu tertulis, pakai nomor baris."
3. **Hook gameplay dulu:** buka dengan tick NP dari kontrol Node, terus "ini mekanik yang seluruh game-nya gantung di situ."

## Klaim yang harus dihindari (penjaga akurasi)

- Jangan bilang tes sudah lulus, game "sudah dikompilasi", atau satu match "sudah selesai" — verifikasi Unity masih tertunda dan belum ada yang dieksekusi.
- Jangan sebut tanggal rilis, harga, atau drop date.
- Jangan janji PvP online; MVP memang single-player by design.
- Tahan sebutan distribusi kartu (11/4/2/3 vs 11/4/3/2 di plan) sampai keputusannya dikunci — bilang "pilot set dua puluh kartu", bukan rincian tipe.
- Jangan klaim "tim developer" manusia berjumlah banyak; timnya memang kecil dan termasuk agent AI.

## Catatan penyuntingan

- Estimasi runtime terukur: pembuka → penutup = 674 kata ≈ **4:30** @ 150 wpm.
- Ada **dua** blok [POTONG] (baris narasi rules §0:40 dan status §2:50). Memotong keduanya memangkas ~135 kata ke ≈ **3:36** — pilihan yang aman kalau mau lebih ringkas.
- Untuk benar-benar mendarat di 3:00–3:30 perlu potongan yang lebih dalam: buang segmen 0:40–1:35 kecuali tiga cara menang, dan rangkum 2:50–3:40 jadi satu kalimat kondisi proyek. Jangan andalkan [POTONG] saja.
- Segmen 1:35–2:50 itu inti emosionalnya; kasih porsi layar paling besar dan visual paling padat.
- Register "gue/lo" bisa ditukar ke "aku/kamu" tanpa mengubah struktur kalimat kalau lo mau nada yang lebih halus.
- Istilah teknis (Node, Core, Upkeep, Protocol, Bench, NP, JSON, Unity) sengaja dibiarkan Inggris karena memang begitu nama di kodenya.
- Rekaman **screen capture only** — tidak ada facecam. Semua visual adalah tangkapan layar aktif: Unity editor, terminal, VS Code, file browser.
- Bagian "kondisi sekarang" paling bagus direkam setelah lock Unity dilepas secara sah — jangan dipaksakan sebelumnya.
- Untuk shot `cards.json` kosong: buka di VS Code atau Notepad, pastikan kontennya terlihat jelas di layar.
