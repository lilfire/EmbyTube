define(['baseView', 'loading', 'toast', 'emby-input', 'emby-button', 'emby-checkbox', 'emby-textarea'], function (BaseView, loading, toast) {
    'use strict';

    var pluginId = '6f1d7a3e-2b8c-4e51-9a0d-3c7e5b2f8a14';

    function escapeHtml(s) {
        return String(s).replace(/[&<>"']/g, function (ch) {
            return { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[ch];
        });
    }

    // The source list comes from the server (PluginConfiguration.AvailableProviders),
    // so new providers appear here without touching this file.
    function renderProviders(view, available, enabled) {
        var html = (available || []).map(function (p) {
            var checked = enabled.indexOf(p.Id) >= 0 ? ' checked' : '';
            return '<div class="checkboxContainer"><label>' +
                '<input is="emby-checkbox" type="checkbox" class="chkProvider" data-provider="' + escapeHtml(p.Id) + '"' + checked + ' />' +
                '<span>' + escapeHtml(p.Name) + '</span></label></div>';
        }).join('');
        view.querySelector('#providerList').innerHTML = html ||
            '<div class="fieldDescription">No sources reported by the server. Restart Emby after updating the plugin.</div>';
    }

    function load(view) {
        loading.show();
        ApiClient.getPluginConfiguration(pluginId).then(function (c) {
            view.querySelector('#chkEnabled').checked = !!c.Enabled;
            renderProviders(view, c.AvailableProviders, c.EnabledProviders || []);
            view.querySelector('#numPageSize').value = c.PageSize;
            view.querySelector('#numMaxPages').value = c.MaxPages;
            view.querySelector('#numMaxCategories').value = c.MaxCategories;
            view.querySelector('#numCategoryMaxPages').value = c.CategoryMaxPages;
            view.querySelector('#txtSavedSearches').value = c.SavedSearches || '';
            view.querySelector('#numMaxHeight').value = c.MaxHeight;
            view.querySelector('#chkPreferHls').checked = !!c.PreferHls;
            view.querySelector('#numTimeout').value = c.RequestTimeoutSeconds;
            view.querySelector('#numCacheMinutes').value = c.MetadataCacheMinutes;
            view.querySelector('#chkDebug').checked = !!c.DebugLogging;
            view.querySelector('#chkXvSfw').checked = !!c.XVideosSfwThumbnails;
            view.querySelector('#txtMockMp4').value = c.MockMp4Url || '';
            view.querySelector('#txtMockHls').value = c.MockHlsUrl || '';
            loading.hide();
        });
    }

    function intValue(view, selector, fallback) {
        var n = parseInt(view.querySelector(selector).value, 10);
        return isNaN(n) ? fallback : n;
    }

    function save(view) {
        loading.show();
        ApiClient.getPluginConfiguration(pluginId).then(function (c) {
            c.Enabled = view.querySelector('#chkEnabled').checked;
            c.EnabledProviders = Array.prototype.filter.call(view.querySelectorAll('.chkProvider'), function (el) {
                return el.checked;
            }).map(function (el) { return el.getAttribute('data-provider'); });
            c.PageSize = intValue(view, '#numPageSize', 30);
            c.MaxPages = intValue(view, '#numMaxPages', 3);
            c.MaxCategories = intValue(view, '#numMaxCategories', 40);
            c.CategoryMaxPages = intValue(view, '#numCategoryMaxPages', 1);
            c.SavedSearches = view.querySelector('#txtSavedSearches').value;
            c.MaxHeight = intValue(view, '#numMaxHeight', 0);
            c.PreferHls = view.querySelector('#chkPreferHls').checked;
            c.RequestTimeoutSeconds = intValue(view, '#numTimeout', 20);
            c.MetadataCacheMinutes = intValue(view, '#numCacheMinutes', 30);
            c.DebugLogging = view.querySelector('#chkDebug').checked;
            c.XVideosSfwThumbnails = view.querySelector('#chkXvSfw').checked;
            c.MockMp4Url = view.querySelector('#txtMockMp4').value.trim();
            c.MockHlsUrl = view.querySelector('#txtMockHls').value.trim();
            return ApiClient.updatePluginConfiguration(pluginId, c);
        }).then(function (result) {
            loading.hide();
            Dashboard.processPluginConfigurationUpdateResult(result);
        }, function () {
            loading.hide();
            toast('Saving EmbyTube settings failed.');
        });
    }

    return class extends BaseView {
        constructor(view, params) {
            super(view, params);
            view.querySelector('.embytubeForm').addEventListener('submit', function (e) {
                e.preventDefault();
                save(view);
                return false;
            });
        }

        onResume(options) {
            super.onResume(options);
            load(this.view);
        }
    };
});
