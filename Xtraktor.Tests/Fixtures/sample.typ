// Test fixture for PdfBookReader. Mimics the layout of a Typst novel interior:
// 6 × 9 in pages, mirrored margins, 0.5 cm first-line indent, no paragraph spacing,
// no hyphenation, large bold uppercase chapter titles, vector scene-break ornament,
// page numbers in the footer. Rebuild with: typst compile sample.typ
#set page(width: 432pt, height: 648pt, margin: (inside: 70pt, outside: 50pt, top: 58pt, bottom: 70pt),
  footer-descent: 40%, numbering: "1")
#set text(lang: "fr", size: 11.5pt, hyphenate: false)
#set par(justify: true, first-line-indent: (amount: 14pt, all: true), spacing: 0.65em, leading: 0.65em)
#show heading: it => { pagebreak(weak: true); v(40pt); align(center, text(16pt, weight: "bold", upper(it.body))); v(40pt) }

#let ornament = align(center, line(length: 124pt, stroke: 2pt))
#let blank = v(0.65em + 11.5pt * 0.75)

#align(center)[Page de titre]

= Prologue

Le train entra en gare avec vingt minutes de retard, sous une pluie fine qui brouillait les lumières du quai et les silhouettes pressées des voyageurs.

— Tu as pensé à prendre les billets ? demanda quelqu'un.

Ils passèrent la soirée à jouer aux _Petits Chevaux_ sur la table de la cuisine, pendant que la radio diffusait une vieille chanson.

= Chapitre 1

Le facteur était passé tôt, comme chaque mardi, et le courrier attendait sur la table. Pourquoi avait-\
elle l'impression que cette lettre-là ne lui apportait rien de bon ?

#ornament

Le lendemain, elle se rendit à la bibliothèque de bonne heure, bien décidée à terminer ses recherches avant midi.

#blank

Pour toujours

Elle resta un long moment sans bouger.

= Épilogue

Le jardinier rangea ses outils et referma la grille derrière lui.

#align(center, text(16pt, weight: "bold")[FIN])

= Remerciements

Merci à toutes celles et tous ceux qui ont lu ce livre.
