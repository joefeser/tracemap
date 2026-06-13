package com.tracemap.jvm.util;

import java.nio.file.Path;

public final class PathsUtil {
    private PathsUtil() {
    }

    public static String relativeUnix(Path root, Path file) {
        return root.toAbsolutePath().normalize().relativize(file.toAbsolutePath().normalize()).toString().replace('\\', '/');
    }

    public static String normalizeUnix(Path path) {
        return path.toString().replace('\\', '/');
    }
}
