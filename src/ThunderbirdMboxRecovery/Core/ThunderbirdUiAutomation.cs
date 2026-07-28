using System.Diagnostics;
using System.Windows.Automation;

namespace ThunderbirdMboxRecovery.Core;

public static class ThunderbirdUiAutomation
{
    public static Task<bool> TrySelectFolderAsync(
        Process process,
        string mailboxName,
        TimeSpan timeout,
        Action<string>? log,
        CancellationToken cancellationToken)
    {
        return Task.Run(() =>
        {
            var deadline = DateTime.UtcNow + timeout;
            AutomationElement? window = null;

            while (DateTime.UtcNow < deadline)
            {
                cancellationToken.ThrowIfCancellationRequested();
                process.Refresh();
                if (process.HasExited) return false;

                if (process.MainWindowHandle != IntPtr.Zero)
                {
                    try
                    {
                        window = AutomationElement.FromHandle(process.MainWindowHandle);
                        if (window is not null) break;
                    }
                    catch
                    {
                        // A janela ainda pode estar em inicialização.
                    }
                }

                Thread.Sleep(500);
            }

            if (window is null)
            {
                log?.Invoke("A janela principal do Thunderbird não ficou disponível para automação.");
                return false;
            }

            log?.Invoke($"Tentando selecionar a pasta '{mailboxName}' pela Automação de Interface do Windows.");
            var treeItemCondition = new PropertyCondition(
                AutomationElement.ControlTypeProperty,
                ControlType.TreeItem);

            while (DateTime.UtcNow < deadline)
            {
                cancellationToken.ThrowIfCancellationRequested();
                AutomationElement? item = null;
                try
                {
                    var items = window.FindAll(TreeScope.Descendants, treeItemCondition);
                    foreach (AutomationElement candidate in items)
                    {
                        var name = candidate.Current.Name?.Trim() ?? string.Empty;
                        if (name.Equals(mailboxName, StringComparison.OrdinalIgnoreCase) ||
                            name.StartsWith(mailboxName + " ", StringComparison.OrdinalIgnoreCase) ||
                            name.StartsWith(mailboxName + " (", StringComparison.OrdinalIgnoreCase))
                        {
                            item = candidate;
                            break;
                        }
                    }
                }
                catch
                {
                    // A árvore pode estar sendo recriada durante a inicialização.
                }

                if (item is not null && TryActivate(item, log))
                    return true;

                TryExpandLocalFolders(window, treeItemCondition, log);
                Thread.Sleep(750);
            }

            log?.Invoke("A pasta não foi localizada automaticamente. O operador pode selecioná-la manualmente na janela isolada do Thunderbird.");
            return false;
        }, cancellationToken);
    }

    private static void TryExpandLocalFolders(
        AutomationElement window,
        Condition treeItemCondition,
        Action<string>? log)
    {
        try
        {
            var items = window.FindAll(TreeScope.Descendants, treeItemCondition);
            foreach (AutomationElement candidate in items)
            {
                var name = candidate.Current.Name?.Trim() ?? string.Empty;
                if (!name.Equals("Pastas Locais", StringComparison.OrdinalIgnoreCase) &&
                    !name.Equals("Local Folders", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (!candidate.TryGetCurrentPattern(ExpandCollapsePattern.Pattern, out var pattern))
                    return;

                var expand = (ExpandCollapsePattern)pattern;
                if (expand.Current.ExpandCollapseState == ExpandCollapseState.Collapsed)
                {
                    expand.Expand();
                    log?.Invoke("A raiz de Pastas Locais foi expandida pela Automação de Interface.");
                }
                return;
            }
        }
        catch (ElementNotAvailableException)
        {
            // A árvore foi atualizada durante a tentativa; o laço principal tentará novamente.
        }
        catch (InvalidOperationException)
        {
            // O item não oferece expansão nesta versão/tema do Thunderbird.
        }
    }

    private static bool TryActivate(AutomationElement item, Action<string>? log)
    {
        try
        {
            item.SetFocus();

            if (item.TryGetCurrentPattern(SelectionItemPattern.Pattern, out var selectionPattern))
            {
                ((SelectionItemPattern)selectionPattern).Select();
                log?.Invoke("Pasta selecionada por SelectionItemPattern.");
                return true;
            }

            if (item.TryGetCurrentPattern(InvokePattern.Pattern, out var invokePattern))
            {
                ((InvokePattern)invokePattern).Invoke();
                log?.Invoke("Pasta acionada por InvokePattern.");
                return true;
            }

            return false;
        }
        catch (ElementNotAvailableException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }
}
