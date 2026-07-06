import json
import sqlite3
from pathlib import Path


JSON_FILE = "german.json"
DB_FILE = "words.db"


CREATE_TABLE_SQL = """
CREATE TABLE IF NOT EXISTS vocab (
    id INTEGER PRIMARY KEY AUTOINCREMENT,

    word TEXT NOT NULL,
    useful_for_flashcard INTEGER NOT NULL DEFAULT 0,
    cefr_level TEXT,
    english_translation TEXT,
    romanization TEXT,

    example_sentence_native TEXT,
    example_sentence_english TEXT,

    gender TEXT,
    is_separable_verb INTEGER NOT NULL DEFAULT 0,
    separable_prefix TEXT,
    base_verb TEXT,

    capitalization_sensitive INTEGER NOT NULL DEFAULT 0,
    pos TEXT,
    word_frequency INTEGER
);
"""


CREATE_INDEXES_SQL = [
    "CREATE INDEX IF NOT EXISTS idx_vocab_word ON vocab(word);",
    "CREATE INDEX IF NOT EXISTS idx_vocab_cefr_level ON vocab(cefr_level);",
    "CREATE INDEX IF NOT EXISTS idx_vocab_pos ON vocab(pos);",
    "CREATE INDEX IF NOT EXISTS idx_vocab_word_frequency ON vocab(word_frequency);",
]


INSERT_SQL = """
INSERT INTO vocab (
    word,
    useful_for_flashcard,
    cefr_level,
    english_translation,
    romanization,
    example_sentence_native,
    example_sentence_english,
    gender,
    is_separable_verb,
    separable_prefix,
    base_verb,
    capitalization_sensitive,
    pos,
    word_frequency
)
VALUES (
    :word,
    :useful_for_flashcard,
    :cefr_level,
    :english_translation,
    :romanization,
    :example_sentence_native,
    :example_sentence_english,
    :gender,
    :is_separable_verb,
    :separable_prefix,
    :base_verb,
    :capitalization_sensitive,
    :pos,
    :word_frequency
);
"""


def bool_to_int(value) -> int:
    """
    Convert JSON true/false values to SQLite-friendly 1/0.
    """
    return 1 if value is True else 0


def empty_to_none(value):
    """
    Convert empty strings to NULL.
    Keep real values unchanged.
    """
    if value == "":
        return None
    return value


def normalize_row(row: dict) -> dict:
    """
    Make sure every JSON row has the exact keys expected by the INSERT query.
    """

    return {
        "word": row.get("word"),
        "useful_for_flashcard": bool_to_int(row.get("useful_for_flashcard")),
        "cefr_level": empty_to_none(row.get("cefr_level")),
        "english_translation": empty_to_none(row.get("english_translation")),
        "romanization": empty_to_none(row.get("romanization")),
        "example_sentence_native": empty_to_none(row.get("example_sentence_native")),
        "example_sentence_english": empty_to_none(row.get("example_sentence_english")),
        "gender": empty_to_none(row.get("gender")),
        "is_separable_verb": bool_to_int(row.get("is_separable_verb")),
        "separable_prefix": empty_to_none(row.get("separable_prefix")),
        "base_verb": empty_to_none(row.get("base_verb")),
        "capitalization_sensitive": bool_to_int(row.get("capitalization_sensitive")),
        "pos": empty_to_none(row.get("pos")),
        "word_frequency": row.get("word_frequency"),
    }


def main() -> None:
    json_path = Path(JSON_FILE)

    if not json_path.exists():
        raise FileNotFoundError(f"Could not find {JSON_FILE}")

    with json_path.open("r", encoding="utf-8") as file:
        data = json.load(file)

    # Supports both:
    # 1. [{...}, {...}]
    # 2. {"word": "...", ...}
    if isinstance(data, dict):
        data = [data]

    if not isinstance(data, list):
        raise ValueError("Expected german.json to contain a JSON object or an array of objects.")

    rows = [normalize_row(item) for item in data]

    bad_rows = [row for row in rows if not row["word"]]
    if bad_rows:
        raise ValueError(f"{len(bad_rows)} rows are missing the required 'word' field.")

    with sqlite3.connect(DB_FILE) as conn:
        # Good settings for importing around 20k rows
        conn.execute("PRAGMA journal_mode = WAL;")
        conn.execute("PRAGMA synchronous = NORMAL;")
        conn.execute("PRAGMA temp_store = MEMORY;")
        conn.execute("PRAGMA foreign_keys = ON;")

        conn.execute(CREATE_TABLE_SQL)

        # One transaction: much faster than committing row by row
        with conn:
            conn.executemany(INSERT_SQL, rows)

        for sql in CREATE_INDEXES_SQL:
            conn.execute(sql)

    print(f"Imported {len(rows)} rows into table 'vocab' in {DB_FILE}.")


if __name__ == "__main__":
    main()