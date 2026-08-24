CREATE DATABASE IF NOT EXISTS thermometrum;

CREATE TABLE IF NOT EXISTS thermometrum.readings
(
    device_id    LowCardinality(String),
    ts           DateTime64(3, 'UTC'),
    temperature  Float32,
    humidity     Float32,
    received_at  DateTime64(3, 'UTC') DEFAULT now64(3)
)
ENGINE = MergeTree
PARTITION BY toYYYYMM(ts)
ORDER BY (device_id, ts);
