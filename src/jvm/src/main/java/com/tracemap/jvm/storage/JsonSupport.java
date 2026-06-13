package com.tracemap.jvm.storage;

import com.fasterxml.jackson.databind.ObjectMapper;
import com.fasterxml.jackson.databind.SerializationFeature;
import com.fasterxml.jackson.databind.json.JsonMapper;
import com.fasterxml.jackson.dataformat.yaml.YAMLFactory;

public final class JsonSupport {
    public static final ObjectMapper JSON = JsonMapper.builder()
        .configure(SerializationFeature.ORDER_MAP_ENTRIES_BY_KEYS, true)
        .disable(SerializationFeature.WRITE_DATES_AS_TIMESTAMPS)
        .build();

    public static final ObjectMapper YAML = new ObjectMapper(new YAMLFactory());

    private JsonSupport() {
    }
}
