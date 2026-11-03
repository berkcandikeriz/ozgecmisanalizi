package com.ozgecmisanalizi.app;

import android.content.Context;
import android.content.SharedPreferences;
import android.net.Uri;

/** Sunucu adresinin saklanması ve doğrulanması. */
final class Prefs {
    private static final String FILE = "settings";
    private static final String KEY_SERVER = "server_url";

    private Prefs() { }

    static String getServer(Context context) {
        SharedPreferences sp = context.getSharedPreferences(FILE, Context.MODE_PRIVATE);
        String saved = sp.getString(KEY_SERVER, null);
        if (saved != null && !saved.isEmpty()) return saved;
        String builtIn = normalize(BuildConfig.DEFAULT_SERVER_URL);
        return builtIn == null ? "" : builtIn;
    }

    static void setServer(Context context, String url) {
        context.getSharedPreferences(FILE, Context.MODE_PRIVATE).edit().putString(KEY_SERVER, url).apply();
    }

    /**
     * Kullanıcının yazdığı adresi "http(s)://host[:port][/yol]" biçimine getirir; geçersizse null döner.
     * Şema yazılmamışsa yerel ağ kullanımı için http varsayılır.
     */
    static String normalize(String input) {
        if (input == null) return null;
        String s = input.trim();
        if (s.isEmpty()) return null;
        if (!s.contains("://")) s = "http://" + s;
        Uri uri = Uri.parse(s);
        String scheme = uri.getScheme() == null ? "" : uri.getScheme().toLowerCase();
        if (!scheme.equals("http") && !scheme.equals("https")) return null;
        if (uri.getHost() == null || uri.getHost().isEmpty()) return null;
        while (s.endsWith("/")) s = s.substring(0, s.length() - 1);
        return s;
    }

    /** Bağlantı adresi uygulamanın kendi sunucusuna mı ait? (şema + host + port) */
    static boolean isSameOrigin(String base, Uri target) {
        if (base == null || base.isEmpty() || target == null) return false;
        Uri b = Uri.parse(base);
        return eq(b.getScheme(), target.getScheme())
                && eq(b.getHost(), target.getHost())
                && port(b) == port(target);
    }

    private static boolean eq(String a, String b) {
        return a != null && a.equalsIgnoreCase(b);
    }

    private static int port(Uri u) {
        if (u.getPort() != -1) return u.getPort();
        return "https".equalsIgnoreCase(u.getScheme()) ? 443 : 80;
    }
}
