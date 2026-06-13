package com.tracemap.jvm.cli;

import com.tracemap.jvm.model.ScanOptions;
import com.tracemap.jvm.model.ScanResult;
import com.tracemap.jvm.scan.ScanEngine;
import java.nio.file.Path;
import java.util.ArrayList;
import java.util.List;

public final class TraceMapJvmCli {
    private TraceMapJvmCli() {
    }

    public static void main(String[] args) {
        try {
            int exit = run(args);
            if (exit != 0) {
                System.exit(exit);
            }
        } catch (Exception exception) {
            System.err.println("tracemap-jvm: " + exception.getMessage());
            System.exit(1);
        }
    }

    static int run(String[] args) throws Exception {
        if (args.length == 0 || has(args, "--help") || has(args, "-h")) {
            printHelp();
            return 0;
        }
        if (has(args, "--version")) {
            System.out.println("tracemap-jvm-mvp");
            return 0;
        }
        if (!"scan".equals(args[0])) {
            System.err.println("Unknown command: " + args[0]);
            printHelp();
            return 2;
        }
        ScanOptions options = parseScan(args);
        ScanResult result = new ScanEngine().scan(options);
        System.out.println("Scan complete");
        System.out.println("Analysis level: " + result.manifest().analysisLevel());
        System.out.println("Build status: " + result.manifest().buildStatus());
        System.out.println("Facts: " + result.facts().size());
        System.out.println("Output: " + options.outputPath().toAbsolutePath().normalize());
        return 0;
    }

    private static ScanOptions parseScan(String[] args) {
        Path repo = null;
        Path out = null;
        List<Path> projects = new ArrayList<>();
        List<String> includes = new ArrayList<>();
        List<String> excludes = new ArrayList<>();
        long maxBytes = 1024L * 1024L;
        boolean semantic = true;
        String language = "all";
        for (int i = 1; i < args.length; i++) {
            String arg = args[i];
            switch (arg) {
                case "--repo" -> repo = Path.of(requireValue(args, ++i, arg));
                case "--out" -> out = Path.of(requireValue(args, ++i, arg));
                case "--project" -> projects.add(Path.of(requireValue(args, ++i, arg)));
                case "--include" -> includes.add(requireValue(args, ++i, arg));
                case "--exclude" -> excludes.add(requireValue(args, ++i, arg));
                case "--max-file-byte-size" -> maxBytes = parseSize(requireValue(args, ++i, arg));
                case "--no-semantic" -> semantic = false;
                case "--language" -> language = requireValue(args, ++i, arg);
                case "--help", "-h" -> {
                    printScanHelp();
                    System.exit(0);
                }
                default -> throw new IllegalArgumentException("Unknown scan option: " + arg);
            }
        }
        if (repo == null || out == null) {
            throw new IllegalArgumentException("scan requires --repo <path> and --out <path>");
        }
        return new ScanOptions(repo, out, projects, includes, excludes, maxBytes, semantic, language);
    }

    private static long parseSize(String value) {
        String lower = value.toLowerCase();
        if (lower.endsWith("mb")) return Long.parseLong(lower.substring(0, lower.length() - 2)) * 1024L * 1024L;
        if (lower.endsWith("kb")) return Long.parseLong(lower.substring(0, lower.length() - 2)) * 1024L;
        return Long.parseLong(lower);
    }

    private static String requireValue(String[] args, int index, String option) {
        if (index >= args.length) {
            throw new IllegalArgumentException(option + " requires a value");
        }
        return args[index];
    }

    private static boolean has(String[] args, String value) {
        for (String arg : args) {
            if (value.equals(arg)) {
                return true;
            }
        }
        return false;
    }

    private static void printHelp() {
        System.out.println("""
            TraceMap JVM scanner

            Usage:
              tracemap-jvm scan --repo <path> --out <path> [options]
              tracemap-jvm --version

            Run 'tracemap-jvm scan --help' for scan options.
            """);
    }

    private static void printScanHelp() {
        System.out.println("""
            Usage:
              tracemap-jvm scan --repo <path> --out <path> [options]

            Options:
              --project <path>              Limit scan to a project/build file or directory (repeatable)
              --include <glob>              Include matching paths
              --exclude <glob>              Exclude matching paths
              --max-file-byte-size <size>   Default: 1mb
              --no-semantic                 Disable Java semantic extraction
              --language <java|kotlin|all>  Default: all
            """);
    }
}
