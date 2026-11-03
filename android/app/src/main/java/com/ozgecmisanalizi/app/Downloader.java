package com.ozgecmisanalizi.app;

import android.app.Activity;
import android.content.ActivityNotFoundException;
import android.content.ContentResolver;
import android.content.ContentValues;
import android.content.Intent;
import android.media.MediaScannerConnection;
import android.net.Uri;
import android.os.Build;
import android.os.Environment;
import android.os.Handler;
import android.os.Looper;
import android.provider.MediaStore;
import android.webkit.CookieManager;
import android.webkit.URLUtil;
import android.widget.Toast;

import java.io.File;
import java.io.FileOutputStream;
import java.io.IOException;
import java.io.InputStream;
import java.io.OutputStream;
import java.net.HttpURLConnection;
import java.net.URL;
import java.net.URLDecoder;
import java.util.Locale;
import java.util.regex.Matcher;
import java.util.regex.Pattern;

/**
 * Sunucunun ürettiği PDF'leri (CV, ön yazı, analiz raporu) WebView oturum çereziyle indirir,
 * İndirilenler/ÖzgeçmişAnalizi klasörüne kaydeder ve uygun bir uygulamayla açar.
 */
final class Downloader {
    static final String FOLDER = "ÖzgeçmişAnalizi";
    private static final Pattern FILENAME_STAR =
            Pattern.compile("filename\\*\\s*=\\s*(?:UTF-8|utf-8)''([^;]+)");

    private Downloader() { }

    static void start(Activity activity, String url, String userAgent, String contentDisposition, String mimeType) {
        Toast.makeText(activity, R.string.download_started, Toast.LENGTH_SHORT).show();
        Handler main = new Handler(Looper.getMainLooper());
        new Thread(() -> {
            try {
                Saved saved = download(activity, url, userAgent, contentDisposition, mimeType);
                main.post(() -> {
                    Toast.makeText(activity, activity.getString(R.string.download_done, saved.name), Toast.LENGTH_LONG).show();
                    open(activity, saved.uri, saved.mime);
                });
            } catch (Exception e) {
                String msg = e.getMessage() == null ? e.getClass().getSimpleName() : e.getMessage();
                main.post(() -> Toast.makeText(activity, activity.getString(R.string.download_failed, msg), Toast.LENGTH_LONG).show());
            }
        }, "pdf-download").start();
    }

    private static final class Saved {
        final Uri uri;
        final String name;
        final String mime;

        Saved(Uri uri, String name, String mime) {
            this.uri = uri;
            this.name = name;
            this.mime = mime;
        }
    }

    private static Saved download(Activity activity, String url, String userAgent, String disposition, String mimeHint)
            throws IOException, InterruptedException {
        HttpURLConnection conn = (HttpURLConnection) new URL(url).openConnection();
        try {
            conn.setConnectTimeout(15000);
            conn.setReadTimeout(180000); // PDF üretimi birkaç saniye sürebilir
            conn.setInstanceFollowRedirects(true);
            String cookies = CookieManager.getInstance().getCookie(url);
            if (cookies != null) conn.setRequestProperty("Cookie", cookies);
            if (userAgent != null) conn.setRequestProperty("User-Agent", userAgent);
            conn.setRequestProperty("Accept", "application/pdf,application/octet-stream,*/*");

            int code = conn.getResponseCode();
            if (code != HttpURLConnection.HTTP_OK) throw new IOException("HTTP " + code);

            String mime = conn.getContentType();
            if (mime != null && mime.toLowerCase(Locale.ROOT).startsWith("text/html")) {
                // Oturum düşmüş ve giriş sayfasına yönlendirilmiş olabilir.
                throw new IOException("oturum süresi dolmuş olabilir, tekrar giriş yapın");
            }
            if (mime == null || mime.isEmpty()) mime = mimeHint;
            if (mime == null || mime.isEmpty()) mime = "application/pdf";
            int semi = mime.indexOf(';');
            if (semi > 0) mime = mime.substring(0, semi).trim();

            String header = conn.getHeaderField("Content-Disposition");
            String name = fileName(header != null ? header : disposition, url, mime);

            try (InputStream in = conn.getInputStream()) {
                return save(activity, in, name, mime);
            }
        } finally {
            conn.disconnect();
        }
    }

    /** ASP.NET Core "filename*=UTF-8''..." alanını tercih eder; Türkçe karakterleri korur. */
    static String fileName(String disposition, String url, String mime) {
        String name = null;
        if (disposition != null) {
            Matcher m = FILENAME_STAR.matcher(disposition);
            if (m.find()) {
                try {
                    name = URLDecoder.decode(m.group(1).trim().replace("+", "%2B"), "UTF-8");
                } catch (Exception ignored) {
                    name = null;
                }
            }
        }
        if (name == null || name.isEmpty()) name = URLUtil.guessFileName(url, disposition, mime);
        name = name.replaceAll("[\\\\/:*?\"<>|\\p{Cntrl}]", "_").trim();
        if (name.isEmpty()) name = "belge";
        if ("application/pdf".equals(mime) && !name.toLowerCase(Locale.ROOT).endsWith(".pdf")) name += ".pdf";
        return name;
    }

    private static Saved save(Activity activity, InputStream in, String name, String mime)
            throws IOException, InterruptedException {
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.Q) {
            ContentResolver resolver = activity.getContentResolver();
            ContentValues values = new ContentValues();
            values.put(MediaStore.MediaColumns.DISPLAY_NAME, name);
            values.put(MediaStore.MediaColumns.MIME_TYPE, mime);
            values.put(MediaStore.MediaColumns.RELATIVE_PATH, Environment.DIRECTORY_DOWNLOADS + "/" + FOLDER);
            values.put(MediaStore.MediaColumns.IS_PENDING, 1);
            Uri uri = resolver.insert(MediaStore.Downloads.EXTERNAL_CONTENT_URI, values);
            if (uri == null) throw new IOException("dosya oluşturulamadı");
            try (OutputStream out = resolver.openOutputStream(uri)) {
                if (out == null) throw new IOException("dosya yazılamadı");
                copy(in, out);
            } catch (IOException e) {
                resolver.delete(uri, null, null);
                throw e;
            }
            values.clear();
            values.put(MediaStore.MediaColumns.IS_PENDING, 0);
            resolver.update(uri, values, null, null);
            return new Saved(uri, name, mime);
        }

        // Android 9 ve altı: genel İndirilenler klasörü (WRITE_EXTERNAL_STORAGE izni MainActivity'de alınır).
        File dir = new File(Environment.getExternalStoragePublicDirectory(Environment.DIRECTORY_DOWNLOADS), FOLDER);
        if (!dir.isDirectory() && !dir.mkdirs()) throw new IOException("klasör oluşturulamadı");
        File file = uniqueFile(dir, name);
        try (OutputStream out = new FileOutputStream(file)) {
            copy(in, out);
        }
        final Uri[] scanned = new Uri[1];
        final Object lock = new Object();
        MediaScannerConnection.scanFile(activity, new String[]{file.getAbsolutePath()}, new String[]{mime},
                (path, uri) -> {
                    synchronized (lock) {
                        scanned[0] = uri;
                        lock.notifyAll();
                    }
                });
        synchronized (lock) {
            if (scanned[0] == null) lock.wait(5000);
        }
        return new Saved(scanned[0], file.getName(), mime);
    }

    private static File uniqueFile(File dir, String name) {
        File f = new File(dir, name);
        if (!f.exists()) return f;
        int dot = name.lastIndexOf('.');
        String base = dot > 0 ? name.substring(0, dot) : name;
        String ext = dot > 0 ? name.substring(dot) : "";
        for (int i = 1; ; i++) {
            f = new File(dir, base + " (" + i + ")" + ext);
            if (!f.exists()) return f;
        }
    }

    private static void copy(InputStream in, OutputStream out) throws IOException {
        byte[] buf = new byte[16 * 1024];
        int n;
        while ((n = in.read(buf)) != -1) out.write(buf, 0, n);
    }

    private static void open(Activity activity, Uri uri, String mime) {
        if (uri == null || activity.isFinishing()) return;
        Intent view = new Intent(Intent.ACTION_VIEW)
                .setDataAndType(uri, mime)
                .addFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION | Intent.FLAG_ACTIVITY_NEW_TASK);
        try {
            activity.startActivity(view);
        } catch (ActivityNotFoundException e) {
            Toast.makeText(activity, R.string.no_app_for_file, Toast.LENGTH_LONG).show();
        }
    }
}
