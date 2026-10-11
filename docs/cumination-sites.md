# Nettsteder i Cumination

Kilde: `plugin.video.cumination/resources/lib/sites/` i https://github.com/dobbelina/repository.dobbelina,
commit `9429c83a` (2026-10-08). Listen er hentet fra `AdultSite(...)`-registreringene i hver modul
(lest som syntakstre, ikke kjørt). Den sier bare at Cumination *har* en modul for nettstedet.
Den sier ingenting om at modulen fungerer i dag, om vilkårene tillater bruk, eller om nettstedet
er egnet for EmbyTube. Hvert nettsted krever egen vurdering før implementering (se `docs/provider-authoring.md`).

- Totalt: **196** registreringer i 169 moduler
- Video-/tube-nettsteder: **185**
- Webkamera (direktesendinger): **11**

Status i EmbyTube: ✅ = implementert, ⚠ = delvis, ✖ = vurdert og ikke implementert.

## Video- og tube-nettsteder

| # | Nettsted | Domene | Cumination-modul | EmbyTube |
|---|---|---|---|---|
| 1 | 12Milf (Dutch) | www.12milf.com | `nltubes.py` || ✖ stream 403 |
| 2 | 3Movs | www.3movs.com | `3movs.py` || ✅ |
| 3 | 6XTube | www.6xtube.com | `6xtube.py` || ✖ |
| 4 | 85po | 85po.com | `85po.py` |  |
| 5 | 8k Porner | 8kporner.com | `8kporner.py` |  |
| 6 | Aag Maal | aagmaal.bz | `aagmaal.py` |  |
| 7 | Aag Maal Pro | aagmaal.farm | `aagmaalpro.py` |  |
| 8 | AbsoluPorn | www.absoluporn.com | `absoluporn.py` || ✖ |
| 9 | AllClassic.Porn | allclassic.porn | `allclassic.py` |  |
| 10 | AllPornStream | allpornstream.com | `AllPornStream.py` |  |
| 11 | Animeid Hentai | animeidhentai.com | `animeidhentai.py` |  |
| 12 | Anybunny | anybunny.org | `anybunny.py` || ✖ |
| 13 | Archivebate | archivebate.com | `archivebate.py` |  |
| 14 | AsianViralHub | asianviralhub.com | `asianviralhub.py` |  |
| 15 | Avjoy | en.avjoy.me | `avjoy.py` |  |
| 16 | Avple | avple.tv | `avple.py` |  |
| 17 | AWM Network - 49 sites | — | `awmnet.py` | ✖ see docs/sites/not-implemented.md |
| 18 | Beeg | store.externulls.com | `beeg.py` || ✖ signed player |
| 19 | BeemTube | beemtube.com | `beemtube.py` || ✖ |
| 20 | Blendporn | www.blendporn.com | `blendporn.py` || ✖ |
| 21 | Camcaps | camcaps.tv | `camcaps.py` |  |
| 22 | CamGirlFap | camgirlfap.com | `camgirlfap.py` |  |
| 23 | CamWhores.tv | www.camwhores.tv | `camwhorestv.py` |  |
| 24 | CamWhoresBay | www.camwhoresbay.com | `camwhoresbay.py` |  |
| 25 | CelebsRoulette | celebsroulette.com | `celebsroulette.py` |  |
| 26 | Cloudbate | www.cloudbate.com | `cloudbate.py` |  |
| 27 | Cum Louder | www.cumlouder.com | `cumlouder.py` || ✅ |
| 28 | Desiporn | desi-porn.tube | `txxx.py` |  |
| 29 | DrTuber | www.drtuber.com | `drtuber.py` || ✅ |
| 30 | Eporner | www.eporner.com | `eporner.py` || ✖ token scheme |
| 31 | EroGarga | www.erogarga.com | `erogarga.py` |  |
| 32 | Erome | www.erome.com | `erome.py` |  |
| 33 | EroticAge | www.eroticage.net | `eroticage.py` |  |
| 34 | EroticMV | eroticmv.com | `eroticmv.py` |  |
| 35 | Familypornhd | familypornhd.com | `familypornhd.py` |  |
| 36 | Foxnxx | foxnxx.com | `foxnxx.py` |  |
| 37 | FPOXXX | www.fpo.xxx | `fpoxxx.py` |  |
| 38 | FreeOMovie | www.freeomovie.to | `freeomovie.py` |  |
| 39 | FreePornVideos | www.freepornvideos.xxx | `freepornvideos.py` || ✖ |
| 40 | Freeuse Porn | www.freeuseporn.com | `freeuseporn.py` || ✅ |
| 41 | FreshPorno | freshporno.org | `freshporno.py` |  |
| 42 | Fullporner | fullporner.org | `fullporner.py` |  |
| 43 | FullTaboo | fulltaboo.tv | `erogarga.py` |  |
| 44 | fullxcinema | fullxcinema.com | `fullxcinema.py` |  |
| 45 | fyxXR | fyxxr.to | `fyxxr.py` |  |
| 46 | hanime.tv | hanime.tv | `hanime.py` |  |
| 47 | HClips | hclips.com | `txxx.py` |  |
| 48 | HD Zog | hdzog.com | `txxx.py` |  |
| 49 | Hdporn92 | hdporn92.com | `hdporn92.py` |  |
| 50 | HeavyFetish | heavyfetish.com | `heavyfetish.py` |  |
| 51 | Hello Porn | hello.porn | `pornhat.py` || ✅ |
| 52 | Hentai Moon | hentai-moon.com | `hentai-moon.py` |  |
| 53 | Hentaidude | hentaidude.xxx | `hentaidude.py` |  |
| 54 | Hentaihaven | hentaihaven.co | `hentaihavenco.py` |  |
| 55 | HentaiStream | hstream.moe | `hentaistream.py` |  |
| 56 | Heroero | heroero.com | `heroero.py` || ✖ |
| 57 | Hitprn | www.hitprn.net | `hitprn.py` |  |
| 58 | Hobby Porn | hobby.porn | `hobbyporn.py` |  |
| 59 | HomeMovies Tube | www.homemoviestube.com | `homemoviestube.py` || ✅ |
| 60 | Homo XXX | homo.xxx | `pornhat.py` || ✅ |
| 61 | Hotmovs | hotmovs.com | `txxx.py` |  |
| 62 | HPJav | hpjav.in | `hpjav.py` |  |
| 63 | HQPorner | hqporner.com | `hqporner.py` |  |
| 64 | HypnoTube | hypnotube.com | `hypnotube.py` || ✅ |
| 65 | IcePornCasting | iceporncasting.net | `iceporncasting.py` |  |
| 66 | Ikisoda | ikisoda.com | `ikisoda.py` |  |
| 67 | Incesto69 | incesto69.com | `Incesto69.py` || ✖ |
| 68 | Inporn | inporn.com | `txxx.py` |  |
| 69 | JapTeenX | www.japteenx.com | `japteenx.py` |  |
| 70 | JAV Bangers | www.javbangers.com | `javbangers.py` |  |
| 71 | Jav Guru | jav.guru | `javguru.py` |  |
| 72 | JAV Moe | javmama.me | `javmoe.py` |  |
| 73 | JavGG | javgg.co | `javgg.py` |  |
| 74 | JavHD Porn | www4.javhdporn.net | `javhdporn.py` |  |
| 75 | Just Full Porn | justfullporn.net | `justfullporn.py` |  |
| 76 | JustPorn | justporn.com | `justporn.py` || ✅ |
| 77 | Kiss JAV | kissjav.com | `kissjav.py` |  |
| 78 | Korean PornMovie | koreanpornmovie.com | `erogarga.py` |  |
| 79 | LuxureTV | en.luxuretv.com | `luxuretv.py` || ✖ fingerprinting |
| 80 | Manysex | manysex.com | `txxx.py` |  |
| 81 | Max Porn | max.porn | `pornhat.py` |  |
| 82 | MegaTube | www.megatube.xxx | `megatube.py` |  |
| 83 | Miss AV | missav123.com | `missav.py` |  |
| 84 | Mr Sexe | www.mrsexe.com | `mrsexe.py` || ✅ |
| 85 | MyPornTape | myporntape.com | `myporntape.py` |  |
| 86 | NaughtyBlog  [Debrid only] | www.naughtyblog.org | `naughtyblog.py` |  |
| 87 | Netfapx | netfapx.com | `netfapx.py` |  |
| 88 | Netflav | netflav.com | `netflav.py` |  |
| 89 | NetflixPorno | netflixporno.net | `netflixporno.py` |  |
| 90 | NonkTube | www.nonktube.com | `nonktube.py` || ✅ |
| 91 | Noodlemagazine | noodlemagazine.com | `noodlemagazine.py` || ✖ |
| 92 | NotFans | notfans.com | `notfans.py` |  |
| 93 | OK Porn | ok.porn | `pornhat.py` || ✅ |
| 94 | OK XXX | ok.xxx | `pornhat.py` || ✅ |
| 95 | OnlyJerk | onlyjerk.net | `onlyjerk.py` |  |
| 96 | ParadiseHill | en.paradisehill.cc | `paradisehill.py` |  |
| 97 | PeekVids | www.peekvids.com | `peekvids.py` || ✖ bot blocking |
| 98 | Perfect Girls | www.perfectgirls.xxx | `pornhat.py` || ✅ |
| 99 | Perverzija | tube.perverzija.com | `perverzija.py` |  |
| 100 | PimpBunny | pimpbunny.com | `pimpbunny.py` |  |
| 101 | PlayVids | www.playvids.com | `playvids.py` || ✅ |
| 102 | PMVHaven | pmvhaven.com | `pmvhaven.py` || ✖ |
| 103 | Poldertube.nl (Dutch) | www.poldertube.nl | `nltubes.py` || ✅ |
| 104 | Porn00 | www.porn00.org | `hdporn.py` |  |
| 105 | Porn4Fans | www.porn4fans.com | `porn4fans.py` |  |
| 106 | Porn4K | porn4k.to | `porn4k.py` |  |
| 107 | Porndig Amateurs | www.porndig.com | `porndig.py` |  |
| 108 | Porndig Professional | www.porndig.com | `porndig.py` |  |
| 109 | Porndish | www.porndish.com | `porndish.py` |  |
| 110 | Pornditt | v.pornditt.com | `pornditt.py` |  |
| 111 | PorneZOO | pornezoo.net | `pornez.py` |  |
| 112 | PornGO | www.porngo.com | `porngo.py` || ✖ |
| 113 | Pornhat | www.pornhat.com | `pornhat.py` || ✅ |
| 114 | PornHoarder | pornhoarder.tv | `pornhoarder.py` |  |
| 115 | PornHub | www.pornhub.com | `pornhub.py` | ⚠ browse only, see docs/sites/pornhub.md |
| 116 | PornKai | pornkai.com | `pornkai.py` |  |
| 117 | Pornmz | pornmz.com | `pornmz.py` |  |
| 118 | Porno1.hu | porno1.hu | `porno1hu.py` || ✅ |
| 119 | Porno365 | m.porno365.pics | `porno365.py` |  |
| 120 | pornobae | pornobae.com | `pornbae.py` |  |
| 121 | PornOne | pornone.com | `pornone.py` || ✅ |
| 122 | PornSlash | www.pornslash.com | `pornslash.py` || ✖ |
| 123 | Pornstars Tube | pornstars.tube | `pornhat.py` |  |
| 124 | PornTN | porntn.com | `porntn.py` |  |
| 125 | PornTrex | www.porntrex.com | `porntrex.py` || ✖ |
| 126 | PornTube.nl (Dutch) | www.porntube.nl | `nltubes.py` || ✖ stream 403 |
| 127 | PornXP | pornxp.com-mirror.com | `pornxp.py` |  |
| 128 | Pornyteen | pornyteen.com | `pornyteen.py` |  |
| 129 | PremiumPorn | premiumporn.org | `premiumporn.py` |  |
| 130 | Reallifecam.to | reallifecam.to | `reallifecam.py` |  |
| 131 | Reallifecams.us | reallifecams.us | `reallifecam.py` |  |
| 132 | Rule34 Video | rule34video.com | `rule34video.py` || ✖ |
| 133 | SeaPorn [Debrid only] | www.seaporn.org | `seaporn.py` |  |
| 134 | SexTB | sextb.net | `sextb.py` |  |
| 135 | Sextube.nl (Dutch) | www.sextube.nl | `nltubes.py` || ✖ stream 403 |
| 136 | ShemaleZ | shemalez.com | `txxx.py` |  |
| 137 | ShowCamRips | www.showcamrips.com | `ShowCamRips.py` |  |
| 138 | SpankBang | spankbang.party | `spankbang.py` | ✖ Cloudflare challenge |
| 139 | SpeedPorn | speedporn.net | `speedporn.py` |  |
| 140 | SuperPorn | www.superporn.com | `superporn.py` || ✅ |
| 141 | SupJav | supjav.com | `supjav.py` |  |
| 142 | Sxy Prn | sxyprn.net | `sxyprn.py` |  |
| 143 | T'nAflix | www.tnaflix.com | `tnaflix.py` || ✅ |
| 144 | TabooFantazy | www.taboofantazy.com | `taboofantazy.py` |  |
| 145 | TabooTube | www.tabootube.xxx | `tabootube.py` || ✅ |
| 146 | Terebon | b.terebon.net | `terebon.py` |  |
| 147 | The Gay | thegay.com | `txxx.py` |  |
| 148 | ThePornRoom | thepornroom.com | `pornroom.py` |  |
| 149 | They Are Huge | www.theyarehuge.com | `theyarehuge.py` |  |
| 150 | thothub | thothub.tube | `thothub.py` |  |
| 151 | TokyoMotion | www.tokyomotion.net | `tokyomotion.py` |  |
| 152 | Trannyteca | www.trannyteca.com | `trannyteca.py` || ✖ |
| 153 | TrendyPorn | www.trendyporn.com | `trendyporn.py` |  |
| 154 | TubePornClassic | tubepornclassic.com | `txxx.py` |  |
| 155 | TubXporn | web.tubxporn.com | `tubxporn.py` || ✖ unreachable |
| 156 | Txxx | txxx.com | `txxx.py` |  |
| 157 | Upornia | upornia.com | `txxx.py` |  |
| 158 | Vagina.nl (Dutch) | vagina.nl | `vaginanl.py` || ✅ |
| 159 | VIP Porns | www.vipporns.com | `vipporns.py` |  |
| 160 | Viral Videos Porno | www.viralvideosporno.com | `viralvideosporno.py` |  |
| 161 | Vjav | vjav.com | `txxx.py` |  |
| 162 | Voyeur-house | www.voyeur-house.me | `reallifecam.py` |  |
| 163 | VoyeurHit | voyeurhit.com | `txxx.py` |  |
| 164 | WatchErotic | watcherotic.com | `watcherotic.py` || ✖ |
| 165 | WatchPorn | watchporn.to | `watchporn.py` |  |
| 166 | WhereIsMyPorn | whereismyporn.com | `whereismyporn.py` |  |
| 167 | WhoresHub | www.whoreshub.com | `whoreshub.py` || ✖ |
| 168 | WOW.xxx | www.wow.xxx | `wowxxx.py` || ✖ bot challenge |
| 169 | X-X-X Video | x-x-x.tube | `xxxtube.py` || ✖ siterips |
| 170 | XFreeHD | beta.xfreehd.com | `xfreehd.py` || ✅ |
| 171 | xHamster | xhamster.com | `xhamster.py` | ✅ |
| 172 | Xmoviesforyou | xmoviesforyou.com | `xmoviesforyou.py` |  |
| 173 | XNXX | www.xnxx.com | `xnxx.py` | ✅ |
| 174 | XOXOstream | xoxostream.com | `xoxostream.py` |  |
| 175 | Xozilla | www.xozilla.com | `xozilla.py` || ✅ |
| 176 | XSharings | xsharings.com | `xsharings.py` |  |
| 177 | XTapes.la | xtapes.la | `xtapesla.py` |  |
| 178 | Xtheatre | pornxtheatre.com | `xtheatre.py` |  |
| 179 | xVideos | www.xvideos.com | `xvideos.py` | ✅ |
| 180 | xxdbx | xxdbx.com | `xxdbx.py` |  |
| 181 | xxThots | xxthots.com | `xxthots.py` |  |
| 182 | YesPorn.vip | yesporn.vip | `yespornvip.py` |  |
| 183 | YouCrazyX | www.youcrazyx.com | `youcrazyx.py` || ✖ |
| 184 | YourLesbians | yourlesbians.com | `yourlesbians.py` |  |
| 185 | YrPrno | www.yrprno.com | `yrprno.py` || ✖ |

## Webkamera (direktesendinger)

Disse er live-strømmer, ikke videoarkiv. De passer dårlig med Embys kanalindeks (ADR-004) og er ikke planlagt.

| # | Nettsted | Domene | Cumination-modul |
|---|---|---|---|
| 1 | Amatuer TV | www.amateur.tv | `amateurtv.py` |
| 2 | BongaCams | bongacams.com | `bongacams.py` |
| 3 | Cam4 | www.cam4.com | `cam4.py` |
| 4 | Camsoda | www.camsoda.com | `camsoda.py` |
| 5 | Chaturbate | chaturbate.com | `chaturbate.py` |
| 6 | MyFreeCams | www.myfreecams.com | `myfreecams.py` |
| 7 | Naked | www.naked.com | `naked.py` |
| 8 | Sinparty | sinparty.com | `sinparty.py` |
| 9 | streamate.com | www.streamate.com | `streamate.py` |
| 10 | stripchat.com | stripchat.com | `stripchat.py` |
| 11 | xLoveCam | www.xlovecam.com | `xlovecam.py` |

## AWM Network (49 nettsteder i modulen `awmnet.py`)

Cumination samler disse nettstedene, som deler samme plattform, i én modul:

4tube.com, aipornvideos.com, analgalore.com, asiangalore.com, assoass.com, bbwpornvideos.com, bigcockxxx.com, biporn.com, cartoonpornvideos.com, coqnu.com, dinotube.com, ebonygalore.com, el-ladies.com, forhertube.com, fucd.com, fullpornvideos.com, fuq.com, fux.com, gaymaletube.com, gotporn.com, hentaigalore.com, homemadegalore.com, ixxx.com, latingalore.com, lesbianpornvideos.com, lobstertube.com, lupoporno.com, maturetube.com, melonstube.com, metaporn.com, modelgalore.com, newporno.com, pornhd.com, pornmd.com, porntv.com, porzo.com, qorno.com, sambaporno.com, shortporn.com, stocking-tease.com, tgtube.com, tiava.com, toroporno.com, tubebdsm.com, tubegalore.com, tubeporn.com, tubepornstars.com, vrxxx.com, xxxmilfs.com
