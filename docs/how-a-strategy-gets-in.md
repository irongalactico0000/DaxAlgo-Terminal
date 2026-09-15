# How a strategy gets in (make vs run)

OMS and Harness do **not** create strategies. They only **run** a unit that already exists.

## If someone gives you a strategy

They must give a **`.daxalgostrategy` file** (Builder **Export open package…**).

1. Menu → **Strategy Manager**
2. **Install open package…** → pick that file
3. Menu → **Harness · Paper…** (or Builder Paper after you still want historical check)
4. Run on **your** Paper book / **your** keys

That is “make it work on this Mac.” We do not paste their C# by hand and we do not copy their live fills.

## If you make it yourself

1. Open **Strategy authoring** (Builder)
2. **Brief**: type what the strategy should do
3. **Build**: generate C# → **Compile & Register**
4. **Validate**: replay that locked version on past bars (quality check)
5. **Paper · Harness**: run on your Paper book

Optional: Charts STOP/TARGET → Send draft, then same Build → Validate → Paper.

## What is finished vs not

| Piece | Finished? |
|-------|-----------|
| Make in Builder (generate → compile → register) | Yes |
| Check quality (historical Validate) | Yes (needs a registered unit + history) |
| Run Paper (Harness / OMS Paper book) | Yes |
| Give/take a file (export + Strategy Manager install) | Yes |
| Prove Alpaca/IB with your real Paper account | Not until you add keys / Gateway |
