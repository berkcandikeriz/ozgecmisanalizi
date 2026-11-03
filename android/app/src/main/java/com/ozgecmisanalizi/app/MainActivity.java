package com.ozgecmisanalizi.app;

import android.Manifest;
import android.annotation.SuppressLint;
import android.app.Activity;
import android.app.AlertDialog;
import android.content.ActivityNotFoundException;
import android.content.ClipData;
import android.content.ClipboardManager;
import android.content.Intent;
import android.content.pm.PackageManager;
import android.graphics.Bitmap;
import android.net.Uri;
import android.net.http.SslError;
import android.os.Build;
import android.os.Bundle;
import android.view.View;
import android.webkit.CookieManager;
import android.webkit.JavascriptInterface;
import android.webkit.SslErrorHandler;
import android.webkit.ValueCallback;
import android.webkit.WebChromeClient;
import android.webkit.WebResourceError;
import android.webkit.WebResourceRequest;
import android.webkit.WebSettings;
import android.webkit.WebView;
import android.webkit.WebViewClient;
import android.widget.LinearLayout;
import android.widget.ProgressBar;
import android.widget.TextView;
import android.widget.Toast;

import java.util.LinkedHashSet;
import java.util.Locale;
import java.util.Set;

/**
 * ÖzgeçmişAnalizi web arayüzünü tam ekran gösteren ana ekran.
 * Sunucu (API + MVC) bilgisayarda çalışır; uygulama ona yerel ağ veya internet üzerinden bağlanır.
 */
public class MainActivity extends Activity {

    private static final int REQ_SETUP = 1;
    private static final int REQ_FILE = 2;
    private static final int REQ_STORAGE = 3;

    private WebView web;
    private ProgressBar progress;
    private LinearLayout errorView;
    private TextView errorDetail;

    private String baseUrl = "";
    private ValueCallback<Uri[]> fileCallback;
    private String[] pendingDownload;
    private boolean mainFrameFailed;

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        setContentView(R.layout.activity_main);
        EdgeToEdge.applySystemBarPadding(findViewById(R.id.root));

        web = findViewById(R.id.web);
        progress = findViewById(R.id.progress);
        errorView = findViewById(R.id.error);
        errorDetail = findViewById(R.id.error_detail);
        findViewById(R.id.error_retry).setOnClickListener(v -> retry());
        findViewById(R.id.error_server).setOnClickListener(v -> openSetup());

        configureWebView();

        baseUrl = Prefs.getServer(this);
        if (baseUrl.isEmpty()) {
            openSetup();
        } else if (savedInstanceState == null || web.restoreState(savedInstanceState) == null) {
            web.loadUrl(baseUrl + "/");
        }
    }

    @SuppressLint({"SetJavaScriptEnabled", "AddJavascriptInterface"})
    private void configureWebView() {
        WebSettings s = web.getSettings();
        s.setJavaScriptEnabled(true);
        s.setDomStorageEnabled(true);
        s.setAllowFileAccess(false);
        s.setAllowContentAccess(true);
        s.setSupportZoom(true);
        s.setBuiltInZoomControls(true);
        s.setDisplayZoomControls(false);
        s.setLoadWithOverviewMode(true);
        s.setUseWideViewPort(true);
        s.setMediaPlaybackRequiresUserGesture(true);
        s.setSupportMultipleWindows(false); // target="_blank" aynı pencerede tetiklenir, aşağıda dışarı yönlendirilir
        s.setMixedContentMode(WebSettings.MIXED_CONTENT_COMPATIBILITY_MODE);
        s.setUserAgentString(s.getUserAgentString() + " OzgecmisAnaliziAndroid/" + BuildConfig.VERSION_NAME);

        CookieManager cookies = CookieManager.getInstance();
        cookies.setAcceptCookie(true);
        cookies.setAcceptThirdPartyCookies(web, true);

        web.addJavascriptInterface(new Bridge(), "OzgecmisApp");
        web.setWebViewClient(new Client());
        web.setWebChromeClient(new Chrome());
        web.setDownloadListener((url, userAgent, contentDisposition, mimeType, contentLength) ->
                download(url, userAgent, contentDisposition, mimeType));
    }

    // ------------------------------------------------------------------ gezinme

    private final class Client extends WebViewClient {
        @Override
        public boolean shouldOverrideUrlLoading(WebView view, WebResourceRequest request) {
            Uri uri = request.getUrl();
            if (Prefs.isSameOrigin(baseUrl, uri)) return false;
            // İlan siteleri, LinkedIn, Kariyer.net, e-posta, telefon vb. telefonun kendi uygulamasında açılır.
            openExternal(uri);
            return true;
        }

        @Override
        public void onPageStarted(WebView view, String url, Bitmap favicon) {
            mainFrameFailed = false;
            progress.setVisibility(View.VISIBLE);
        }

        @Override
        public void onPageFinished(WebView view, String url) {
            progress.setVisibility(View.GONE);
            CookieManager.getInstance().flush();
            if (!mainFrameFailed) errorView.setVisibility(View.GONE);
            // Düz HTTP "güvenli bağlam" sayılmadığından navigator.clipboard yoktur; yerel köprüyle sağlanır.
            view.evaluateJavascript(
                    "(function(){if(window.OzgecmisApp&&!(navigator.clipboard&&navigator.clipboard.writeText)){"
                            + "try{Object.defineProperty(navigator,'clipboard',{configurable:true,value:{writeText:function(t){"
                            + "OzgecmisApp.copyText(String(t));return Promise.resolve();}}});}catch(e){}}})();", null);
        }

        @Override
        public void onReceivedError(WebView view, WebResourceRequest request, WebResourceError error) {
            if (request.isForMainFrame()) {
                showError(error.getDescription() + "\n" + baseUrl);
            }
        }

        @SuppressLint("WebViewClientOnReceivedSslError")
        @Override
        public void onReceivedSslError(WebView view, SslErrorHandler handler, SslError error) {
            handler.cancel();
            showError(getString(R.string.error_ssl));
        }
    }

    private void showError(String detail) {
        mainFrameFailed = true;
        progress.setVisibility(View.GONE);
        errorDetail.setText(detail);
        errorView.setVisibility(View.VISIBLE);
    }

    private void retry() {
        errorView.setVisibility(View.GONE);
        String current = web.getUrl();
        if (current == null || current.startsWith("data:") || current.equals("about:blank")) {
            web.loadUrl(baseUrl + "/");
        } else {
            web.reload();
        }
    }

    private void openExternal(Uri uri) {
        Intent intent;
        if ("intent".equalsIgnoreCase(uri.getScheme())) {
            try {
                intent = Intent.parseUri(uri.toString(), Intent.URI_INTENT_SCHEME);
                intent.addCategory(Intent.CATEGORY_BROWSABLE);
                intent.setComponent(null);
                intent.setSelector(null);
            } catch (Exception e) {
                return;
            }
        } else {
            intent = new Intent(Intent.ACTION_VIEW, uri).addCategory(Intent.CATEGORY_BROWSABLE);
        }
        try {
            startActivity(intent);
        } catch (ActivityNotFoundException e) {
            Toast.makeText(this, R.string.no_app_for_file, Toast.LENGTH_SHORT).show();
        }
    }

    // ------------------------------------------------------------------ dosya seçme (CV yükleme)

    private final class Chrome extends WebChromeClient {
        @Override
        public void onProgressChanged(WebView view, int newProgress) {
            progress.setProgress(newProgress);
        }

        @Override
        public boolean onShowFileChooser(WebView view, ValueCallback<Uri[]> callback, FileChooserParams params) {
            if (fileCallback != null) fileCallback.onReceiveValue(null);
            fileCallback = callback;

            String[] mimes = mimeTypes(params.getAcceptTypes());
            Intent intent = new Intent(Intent.ACTION_OPEN_DOCUMENT)
                    .addCategory(Intent.CATEGORY_OPENABLE)
                    .setType(mimes.length == 1 ? mimes[0] : "*/*");
            if (mimes.length > 1) intent.putExtra(Intent.EXTRA_MIME_TYPES, mimes);
            if (params.getMode() == FileChooserParams.MODE_OPEN_MULTIPLE) {
                intent.putExtra(Intent.EXTRA_ALLOW_MULTIPLE, true);
            }
            try {
                startActivityForResult(intent, REQ_FILE);
            } catch (ActivityNotFoundException e) {
                fileCallback = null;
                return false;
            }
            return true;
        }
    }

    /** accept=".pdf,.docx,.txt,application/pdf,..." değerini Android MIME türlerine çevirir. */
    static String[] mimeTypes(String[] accept) {
        Set<String> out = new LinkedHashSet<>();
        if (accept != null) {
            for (String group : accept) {
                if (group == null) continue;
                for (String raw : group.split(",")) {
                    String a = raw.trim().toLowerCase(Locale.ROOT);
                    if (a.isEmpty()) continue;
                    switch (a) {
                        case ".pdf": out.add("application/pdf"); break;
                        case ".docx": out.add("application/vnd.openxmlformats-officedocument.wordprocessingml.document"); break;
                        case ".doc": out.add("application/msword"); break;
                        case ".txt": out.add("text/plain"); break;
                        default: if (a.contains("/")) out.add(a);
                    }
                }
            }
        }
        if (out.isEmpty()) out.add("*/*");
        return out.toArray(new String[0]);
    }

    // ------------------------------------------------------------------ PDF indirme

    private void download(String url, String userAgent, String disposition, String mime) {
        if (Build.VERSION.SDK_INT < Build.VERSION_CODES.Q
                && checkSelfPermission(Manifest.permission.WRITE_EXTERNAL_STORAGE) != PackageManager.PERMISSION_GRANTED) {
            pendingDownload = new String[]{url, userAgent, disposition, mime};
            requestPermissions(new String[]{Manifest.permission.WRITE_EXTERNAL_STORAGE}, REQ_STORAGE);
            return;
        }
        Downloader.start(this, url, userAgent, disposition, mime);
    }

    @Override
    public void onRequestPermissionsResult(int requestCode, String[] permissions, int[] grantResults) {
        super.onRequestPermissionsResult(requestCode, permissions, grantResults);
        if (requestCode != REQ_STORAGE) return;
        String[] p = pendingDownload;
        pendingDownload = null;
        if (grantResults.length > 0 && grantResults[0] == PackageManager.PERMISSION_GRANTED && p != null) {
            Downloader.start(this, p[0], p[1], p[2], p[3]);
        } else {
            Toast.makeText(this, R.string.download_permission, Toast.LENGTH_LONG).show();
        }
    }

    // ------------------------------------------------------------------ JS köprüsü

    private final class Bridge {
        @JavascriptInterface
        public void copyText(String text) {
            runOnUiThread(() -> {
                ClipboardManager cm = (ClipboardManager) getSystemService(CLIPBOARD_SERVICE);
                if (cm != null) {
                    cm.setPrimaryClip(ClipData.newPlainText("ÖzgeçmişAnalizi", text));
                    if (Build.VERSION.SDK_INT < Build.VERSION_CODES.TIRAMISU) {
                        Toast.makeText(MainActivity.this, R.string.copied, Toast.LENGTH_SHORT).show();
                    }
                }
            });
        }
    }

    // ------------------------------------------------------------------ ayarlar / yaşam döngüsü

    private void openSetup() {
        startActivityForResult(new Intent(this, SetupActivity.class), REQ_SETUP);
    }

    @Override
    protected void onActivityResult(int requestCode, int resultCode, Intent data) {
        super.onActivityResult(requestCode, resultCode, data);
        if (requestCode == REQ_FILE) {
            if (fileCallback != null) {
                fileCallback.onReceiveValue(WebChromeClient.FileChooserParams.parseResult(resultCode, data));
                fileCallback = null;
            }
        } else if (requestCode == REQ_SETUP) {
            String server = Prefs.getServer(this);
            if (server.isEmpty()) {
                finish(); // adres girilmeden kurulumdan çıkıldı
                return;
            }
            if (resultCode == RESULT_OK || !server.equals(baseUrl)) {
                baseUrl = server;
                errorView.setVisibility(View.GONE);
                web.clearHistory();
                web.loadUrl(baseUrl + "/");
            }
        }
    }

    @Override
    public void onBackPressed() {
        if (errorView.getVisibility() == View.VISIBLE && web.canGoBack()) {
            errorView.setVisibility(View.GONE);
            web.goBack();
        } else if (web.canGoBack()) {
            web.goBack();
        } else {
            showMenu();
        }
    }

    /** Geçmişin başında geri tuşu: yenile, sunucu değiştir veya çık. */
    private void showMenu() {
        String[] items = {
                getString(R.string.menu_home),
                getString(R.string.menu_reload),
                getString(R.string.menu_server),
                getString(R.string.menu_exit)
        };
        new AlertDialog.Builder(this)
                .setTitle(getString(R.string.menu_title) + "  ·  " + baseUrl)
                .setItems(items, (d, which) -> {
                    switch (which) {
                        case 0: errorView.setVisibility(View.GONE); web.loadUrl(baseUrl + "/"); break;
                        case 1: retry(); break;
                        case 2: openSetup(); break;
                        default: finish();
                    }
                })
                .show();
    }

    @Override
    protected void onSaveInstanceState(Bundle outState) {
        super.onSaveInstanceState(outState);
        web.saveState(outState);
    }

    @Override
    protected void onResume() {
        super.onResume();
        web.onResume();
    }

    @Override
    protected void onPause() {
        web.onPause();
        CookieManager.getInstance().flush();
        super.onPause();
    }

    @Override
    protected void onDestroy() {
        if (fileCallback != null) {
            fileCallback.onReceiveValue(null);
            fileCallback = null;
        }
        web.destroy();
        super.onDestroy();
    }
}
