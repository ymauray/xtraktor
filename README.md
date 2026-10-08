# Xtraktor

[![Build .NET](https://github.com/ymauray/xtraktor/actions/workflows/dotnet.yml/badge.svg)](https://github.com/ymauray/xtraktor/actions/workflows/dotnet.yml)
[![Licence MIT](https://img.shields.io/badge/Licence-MIT-yellow.svg)](LICENSE)

Xtraktor est un outil en ligne de commande (CLI) développé en .NET 10 qui extrait le texte d'un roman publié (PDF d'impression, EPUB ou manuscrit Word), compare les versions entre elles et reconstitue un manuscrit Word (`.docx`).

C'est le chemin inverse de [Johannes](https://github.com/ymauray/johannes) et [Paige](https://github.com/ymauray/paige) : il permet de retrouver le texte source d'un livre quand on ne dispose plus que des fichiers publiés, et de vérifier que les différentes éditions (broché, relié, ebook) contiennent bien le même texte.

## Fonctionnalités

- **Extraction** depuis :
  - un **PDF** généré par Typst : la structure est reconstruite à partir de la géométrie de la page (retrait de première ligne, titres en grand corps, ornements de séparation de scène, lignes vides, numéros de page ignorés, traits d'union de fin de ligne recollés) ;
  - un **EPUB** : lecture des documents dans l'ordre de lecture (spine) ;
  - un **manuscrit Word** utilisant les styles `Titre 1`, `Titre`, `Ellipse` et `Normal`.
- **Plage du récit** : par défaut, seul le texte du prologue à la fin de l'épilogue est conservé (`--all` pour garder tout le livre).
- **Comparaison** de deux sources, avec un rapport HTML qui distingue :
  - les différences de texte (diff au mot près) ;
  - les différences de mise en forme (italique, gras) ;
  - les lignes vides présentes d'un seul côté ;
  - les différences purement typographiques (espaces insécables, apostrophes, casse des titres).
- **Reconstitution d'un manuscrit Word** : `Titre 1` pour les chapitres, `Normal` pour le texte, `Ellipse` pour les séparateurs de scène, `Titre` pour le mot « FIN » ; italique et gras en mise en forme directe. Mise en page A4, marges de 2,5 cm, Georgia, interligne 1,5.
- **Modèle intermédiaire JSON**, lisible et modifiable à la main.

## Installation

### Homebrew (macOS Apple Silicon, Linux x64)

```bash
brew install ymauray/tap/xtraktor
```

### Scoop (Windows x64)

```powershell
scoop bucket add ymauray https://github.com/ymauray/scoop-bucket
scoop install xtraktor
```

### Binaires et paquets

Chaque [release](https://github.com/ymauray/xtraktor/releases) fournit un binaire autonome (aucun runtime .NET requis) pour macOS (`osx-arm64`), Linux (`linux-x64`, aussi en `.deb` et `.rpm`) et Windows (`win-x64`).

### Depuis les sources

```bash
git clone https://github.com/ymauray/xtraktor.git
cd xtraktor
dotnet build
```

## Utilisation

```bash
# Extraire le texte en JSON
xtraktor extract livre.pdf -o livre.json

# Comparer deux sources (pdf, epub, docx ou json), avec un rapport HTML
xtraktor diff broché.pdf relié.pdf -o rapport.html
xtraktor diff livre.pdf livre.epub -o rapport.html

# Reconstituer un manuscrit Word
xtraktor docx livre.epub -o manuscrit.docx
```

Options :

| Option | Commande | Effet |
| --- | --- | --- |
| `-o <fichier>` | toutes | Fichier de sortie |
| `--all` | toutes | Garde tout le livre au lieu de la plage prologue → épilogue |
| `--scene-style <nom>` | `docx` | Nom du style des séparateurs de scène (`Ellipse` par défaut) |
| `--no-fin` | `docx` | N'écrit pas le mot « FIN » final |
| `--version` | | Affiche la version |

La commande `diff` affiche un résumé dans le terminal, par exemple :

```text
A : broché.pdf — 3790 blocs
B : livre.epub — 3790 blocs
Différences de texte        : 0
Différences de mise en forme : 0
Différences typographiques  : 2357
Lignes vides d'un seul côté : 7
     2313 × + espace insécable, − espace
       44 × casse
```

## Tests

```bash
dotnet test
```

Les tests (xUnit) s'appuient uniquement sur des fichiers d'exemple au texte factice. Le PDF d'exemple `Xtraktor.Tests/Fixtures/sample.pdf` est compilé à partir de `sample.typ` :

```bash
typst compile Xtraktor.Tests/Fixtures/sample.typ
```

## Contribution

La branche `main` est protégée. Créez une branche dédiée pour chaque modification et intégrez-la via une pull request. Les messages de commit suivent [Conventional Commits](https://www.conventionalcommits.org/), en français.

Pour publier une version, poussez un tag `vX.Y.Z` : le workflow de release exécute les tests, construit les binaires, crée la release GitHub et ouvre les pull requests de mise à jour de la formule Homebrew et du manifeste Scoop.

## Architecture

- `Book`, `Block`, `Run` : modèle commun du livre (titres, paragraphes, séparateurs, lignes vides, segments en italique ou en gras).
- `PdfBookReader`, `EpubBookReader`, `DocxBookReader` : lecture de chaque format vers le modèle.
- `BookDiff` : alignement des blocs, classement des différences et rapport HTML.
- `DocxWriter` : écriture du manuscrit Word.

## Licence

Ce projet est sous licence MIT. Voir le fichier [LICENSE](LICENSE) pour plus de détails.
