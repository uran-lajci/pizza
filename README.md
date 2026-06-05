# Running the Pizza Solver (Ubuntu / Mono)

This is a .NET Framework 4.6.1 project that uses `System.Drawing` to produce a
visualization image. On Linux it runs under **Mono**.

## Prerequisites

```bash
sudo apt update
sudo apt install -y mono-complete libgdiplus
```

- `mono-complete` provides the runtime and build tools (`xbuild`/`msbuild`).
- `libgdiplus` is **required** — without it the `.png` generation throws a
  `System.Drawing` error at runtime.

## Build

From the repository root:

```bash
xbuild HashCode_Pizza.sln /p:Configuration=Release
```

(`xbuild` is deprecated but works fine here. If your Mono has `msbuild`, use that
instead.)

The compiled executable is produced at:

```
HashCode_Pizza/bin/Release/HashCode_Pizza.exe
```

## Run

The program takes a **single argument**: the path to a Hash Code pizza input
file.

```bash
mono HashCode_Pizza/bin/Release/HashCode_Pizza.exe path/to/input.in
```

It prints the score to the console, for example:

```
Max theoretical score: 1000000
Solution score: 909569
```

It also writes two files next to the input:

- `input.in.out` — the submission file (slice coordinates).
- `input.in.png` — a color visualization of the slicing.

If the solution is malformed, the program prints `ERROR: Invalid slicing`.

## Input format

First line: `rows columns minIngredientsPerSlice maxSliceSize`, followed by
`rows` lines of `T` (tomato) and `M` (mushroom) characters.

Quick test:

```bash
cat > test.in <<'EOF'
3 5 1 6
TTTTT
TMMMT
TTTTT
EOF
mono HashCode_Pizza/bin/Release/HashCode_Pizza.exe test.in
```

## Notes

- **Deterministic:** the algorithm has no randomness or parallelism, so the same
  input always produces the same score and output.
- **Runtime:** all 2017 practice instances (including big, 1000×1000) finish in
  seconds — well under any 10-minute limit.

# Theoretical Max Scores

Example 15
Small 42
Medium 50,000
Big 1,000,000

