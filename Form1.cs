using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Diagnostics;

namespace changeWeChat
{
    public partial class Form1 : Form
    {
        // Win32 API
        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll")]
        public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        [DllImport("user32.dll")]
        public static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll")]
        public static extern int GetClassName(IntPtr hWnd, System.Text.StringBuilder lpClassName, int nMaxCount);

        [DllImport("user32.dll")]
        public static extern bool EnumWindows(EnumWindowsProc enumProc, IntPtr lParam);

        [DllImport("user32.dll")]
        public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll")]
        public static extern bool IsWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern bool SetLayeredWindowAttributes(IntPtr hwnd, uint crKey, byte bAlpha, uint dwFlags);

        public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        public struct RECT { public int Left, Top, Right, Bottom; }

        // 常量定义
        private const int WS_EX_LAYERED = 0x80000;
        private const int WS_EX_TRANSPARENT = 0x20;
        private const int WS_EX_TOPMOST = 0x8;
        private const int LWA_ALPHA = 0x2;
        private const int HOTKEY_ID = 9000;
        private const int HOTKEY_ID_CHAT = 9001; // 新增：专门控制聊天区域透明度的热键
        private const uint MOD_CONTROL = 0x0002;
        private const uint MOD_SHIFT = 0x0004;
        private const uint MOD_ALT = 0x0001;
        private const uint VK_W = 0x57;
        private const uint VK_C = 0x43; // C键用于控制聊天区域透明度

        System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer();
        IntPtr hWeChat = IntPtr.Zero;
        private bool isTransparent = true; // 透明模式状态
        private int chatAreaAlpha = 60; // 聊天区域透明度 (0-255)

        public Form1()
        {
            InitializeComponent();
            this.FormBorderStyle = FormBorderStyle.None;
            this.TopMost = true;
            this.ShowInTaskbar = false;
            this.DoubleBuffered = true;
            this.StartPosition = FormStartPosition.Manual;
            this.BackColor = Color.White;

            // 定时器设置
            timer.Interval = 500; // 500ms检查一次
            timer.Tick += (s, e) => SyncWithWeChat();
            timer.Start();

            // 启动时主动查找微信窗口
            FindWeChatWindow();

            // 注册热键 Ctrl+Shift+W 和 Ctrl+Alt+C
            RegisterHotKey(this.Handle, HOTKEY_ID, MOD_CONTROL | MOD_SHIFT, VK_W);
            RegisterHotKey(this.Handle, HOTKEY_ID_CHAT, MOD_CONTROL | MOD_ALT, VK_C);

            if (hWeChat == IntPtr.Zero)
            {
                MessageBox.Show("未找到微信窗口，请先启动微信PC版！\n\n程序将继续运行，定时查找微信窗口。\n\n" +
                    "按 Ctrl+Shift+W 切换透明模式\n按 Ctrl+Alt+C 调整聊天区域透明度", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                // 居中显示遮罩层，方便查看效果
                this.SetBounds((Screen.PrimaryScreen.WorkingArea.Width - 800) / 2,
                              (Screen.PrimaryScreen.WorkingArea.Height - 450) / 2, 800, 450);
            }

            // 设置透明模式
            SetTransparentMode(isTransparent);
        }

        // 改进的微信窗口查找方法
        void FindWeChatWindow()
        {
            hWeChat = IntPtr.Zero;

            // 尝试多种可能的类名
            string[] possibleClassNames = {
                "WeChatMainWndForPC",
                "WeChat",
                "ChatWnd",
                "WeChatMaster",
                "WeChatWindow"
            };

            foreach (string className in possibleClassNames)
            {
                hWeChat = FindWindow(className, null);
                if (hWeChat != IntPtr.Zero)
                {
                    Console.WriteLine($"找到微信窗口，类名：{className}");
                    break;
                }
            }

            // 如果还没找到，尝试通过进程名查找
            if (hWeChat == IntPtr.Zero)
            {
                hWeChat = FindWeChatByProcess();
            }

            // 验证找到的窗口是否有效
            if (hWeChat != IntPtr.Zero && !IsWindow(hWeChat))
            {
                hWeChat = IntPtr.Zero;
            }
        }

        // 通过进程名查找微信窗口
        IntPtr FindWeChatByProcess()
        {
            IntPtr weChatHwnd = IntPtr.Zero;

            try
            {
                Process[] processes = Process.GetProcessesByName("WeChat");
                if (processes.Length == 0)
                {
                    processes = Process.GetProcessesByName("wechat");
                }

                foreach (Process process in processes)
                {
                    if (process.MainWindowHandle != IntPtr.Zero)
                    {
                        // 获取窗口标题
                        System.Text.StringBuilder windowTitle = new System.Text.StringBuilder(256);
                        GetWindowText(process.MainWindowHandle, windowTitle, 256);

                        Console.WriteLine($"找到微信窗口：{windowTitle} - Handle: {process.MainWindowHandle}");

                        // 检查窗口大小
                        RECT rect;
                        if (GetWindowRect(process.MainWindowHandle, out rect))
                        {
                            int width = rect.Right - rect.Left;
                            int height = rect.Bottom - rect.Top;

                            if (width > 300 && height > 200) // 主窗口通常比较大
                            {
                                weChatHwnd = process.MainWindowHandle;
                                Console.WriteLine($"选择此窗口作为微信主窗口，大小：{width}x{height}");
                                break;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"查找微信进程时出错：{ex.Message}");
            }

            return weChatHwnd;
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= WS_EX_LAYERED | WS_EX_TOPMOST;
                if (isTransparent)
                {
                    cp.ExStyle |= WS_EX_TRANSPARENT;
                }
                return cp;
            }
        }

        private void SetTransparentMode(bool transparent)
        {
            isTransparent = transparent;

            // 重新创建窗口以应用新的扩展样式
            this.RecreateHandle();

            // 设置窗口透明度
            SetLayeredWindowAttributes(this.Handle, 0, transparent ? (byte)200 : (byte)255, LWA_ALPHA);

            this.Invalidate(); // 刷新界面
        }

        void SyncWithWeChat()
        {
            if (hWeChat == IntPtr.Zero || !IsWindow(hWeChat))
            {
                FindWeChatWindow();
            }

            if (hWeChat != IntPtr.Zero && IsWindow(hWeChat) && IsWindowVisible(hWeChat))
            {
                RECT rect;
                if (GetWindowRect(hWeChat, out rect))
                {
                    int width = rect.Right - rect.Left;
                    int height = rect.Bottom - rect.Top;

                    if (width > 0 && height > 0)
                    {
                        // 同步位置和大小
                        SetWindowPos(this.Handle, new IntPtr(-1), rect.Left, rect.Top, width, height, 0x0010);

                        // 如果窗口大小变化，重新绘制
                        if (this.Width != width || this.Height != height)
                        {
                            this.Invalidate();
                        }
                    }
                }
            }
            else
            {
                // 没找到微信时，遮罩层居中显示
                this.SetBounds((Screen.PrimaryScreen.WorkingArea.Width - 800) / 2,
                              (Screen.PrimaryScreen.WorkingArea.Height - 450) / 2, 800, 450);
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            try
            {
                var imgPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "zhaohu.png");
                if (System.IO.File.Exists(imgPath))
                {
                    using (Image img = Image.FromFile(imgPath))
                    {
                        e.Graphics.DrawImage(img, 0, 0, this.Width, this.Height);
                    }
                }
                else
                {
                    // 创建一个简单的招呼界面模拟
                    e.Graphics.Clear(Color.FromArgb(245, 245, 245));

                    // 模拟招呼软件的顶部标题栏
                    using (Brush titleBrush = new SolidBrush(Color.FromArgb(64, 158, 255)))
                    {
                        e.Graphics.FillRectangle(titleBrush, 0, 0, this.Width, 50);
                    }

                    // 绘制招呼标题和图标
                    using (Font titleFont = new Font("微软雅黑", 16, FontStyle.Bold))
                    {
                        e.Graphics.DrawString("招呼", titleFont, Brushes.White, new PointF(15, 15));
                    }

                    // 模拟左侧联系人列表
                    using (Brush sidebarBrush = new SolidBrush(Color.FromArgb(230, 230, 230)))
                    {
                        e.Graphics.FillRectangle(sidebarBrush, 0, 50, this.Width / 4, this.Height - 50);
                    }

                    // 模拟聊天内容区域
                    using (Brush chatBrush = new SolidBrush(Color.White))
                    {
                        e.Graphics.FillRectangle(chatBrush, this.Width / 4, 50, this.Width * 3 / 4, this.Height - 50);
                    }

                    // 绘制一些模拟的聊天内容
                    using (Font contentFont = new Font("微软雅黑", 10))
                    {
                        // 左侧联系人
                        e.Graphics.DrawString("工作群聊", contentFont, Brushes.Black, new PointF(10, 70));
                        e.Graphics.DrawString("项目讨论", contentFont, Brushes.Black, new PointF(10, 95));
                        e.Graphics.DrawString("技术交流", contentFont, Brushes.Black, new PointF(10, 120));

                        // 右侧聊天内容
                        int chatX = this.Width / 4 + 20;
                        e.Graphics.DrawString("张三：今天的项目进度如何？", contentFont, Brushes.Black, new PointF(chatX, 70));
                        e.Graphics.DrawString("李四：基本完成了，正在测试", contentFont, Brushes.Black, new PointF(chatX, 95));
                        e.Graphics.DrawString("王五：我这边也差不多了", contentFont, Brushes.Black, new PointF(chatX, 120));
                    }

                    // 显示当前状态
                    string statusText = isTransparent ? "透明模式：开启" : "透明模式：关闭";
                    using (Font statusFont = new Font("微软雅黑", 12, FontStyle.Bold))
                    {
                        e.Graphics.DrawString(statusText, statusFont,
                            isTransparent ? Brushes.Green : Brushes.Red,
                            new PointF(this.Width - 150, 10));
                    }

                    // 热键提示
                    using (Font tipFont = new Font("微软雅黑", 10))
                    {
                        e.Graphics.DrawString("按 Ctrl+Shift+W 切换透明模式", tipFont, Brushes.Blue,
                            new PointF(10, this.Height - 40));
                    }
                }

                // 在透明模式下，为聊天内容区域添加半透明遮罩
                if (isTransparent)
                {
                    // 计算聊天内容区域（类似微信右侧聊天区域）
                    int chatAreaX = this.Width / 4;
                    int chatAreaY = 50;
                    int chatAreaWidth = this.Width * 3 / 4;
                    int chatAreaHeight = this.Height - 50;

                    // 绘制半透明遮罩，让下面的微信内容更清晰
                    using (Brush maskBrush = new SolidBrush(Color.FromArgb(60, 245, 245, 245)))
                    {
                        e.Graphics.FillRectangle(maskBrush, chatAreaX, chatAreaY, chatAreaWidth, chatAreaHeight);
                    }

                    // 在遮罩区域添加一个边框提示
                    using (Pen borderPen = new Pen(Color.FromArgb(100, 64, 158, 255), 2))
                    {
                        e.Graphics.DrawRectangle(borderPen, chatAreaX, chatAreaY, chatAreaWidth - 1, chatAreaHeight - 1);
                    }
                    // 微信窗口状态信息
                    string weChatStatus1 = hWeChat == IntPtr.Zero ? "未找到微信窗口" : "已找到微信窗口";
                    using (Font statusFont = new Font("微软雅黑", 10))
                    {
                        e.Graphics.DrawString(weChatStatus1, statusFont,
                            hWeChat == IntPtr.Zero ? Brushes.Red : Brushes.Green,
                            new PointF(10, this.Height - 25));
                    }
                }
            }
            catch (Exception ex)
            {
                e.Graphics.Clear(Color.White);
                e.Graphics.DrawString($"显示异常：{ex.Message}", new Font("微软雅黑", 12), Brushes.Red, new PointF(10, 10));
            }

            // 微信窗口状态信息
            string weChatStatus = hWeChat == IntPtr.Zero ? "未找到微信窗口" : "已找到微信窗口";
            using (Font statusFont = new Font("微软雅黑", 10))
            {
                e.Graphics.DrawString(weChatStatus, statusFont,
                    hWeChat == IntPtr.Zero ? Brushes.Red : Brushes.Green,
                    new PointF(10, this.Height - 20));
            }
        }

        protected override void WndProc(ref Message m)
        {
            const int WM_HOTKEY = 0x0312;

            if (m.Msg == WM_HOTKEY)
            {
                if (m.WParam.ToInt32() == HOTKEY_ID)
                {
                    // 切换透明模式
                    SetTransparentMode(!isTransparent);
                    return;
                }
                else if (m.WParam.ToInt32() == HOTKEY_ID_CHAT)
                {
                    // 调整聊天区域透明度
                    AdjustChatAreaAlpha();
                    return;
                }
            }

            base.WndProc(ref m);
        }

        private void AdjustChatAreaAlpha()
        {
            // 循环调整透明度：30 -> 60 -> 90 -> 120 -> 150 -> 30
            chatAreaAlpha += 30;
            if (chatAreaAlpha > 150)
            {
                chatAreaAlpha = 30;
            }

            // 重新绘制
            this.Invalidate();
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            timer.Stop();
            UnregisterHotKey(this.Handle, HOTKEY_ID);
            UnregisterHotKey(this.Handle, HOTKEY_ID_CHAT);
            base.OnFormClosed(e);
        }

        // 添加右键菜单支持（仅在非透明模式下可用）
        protected override void OnMouseClick(MouseEventArgs e)
        {
            if (!isTransparent && e.Button == MouseButtons.Right)
            {
                ContextMenuStrip menu = new ContextMenuStrip();
                menu.Items.Add("重新查找微信", null, (s, args) => FindWeChatWindow());
                menu.Items.Add($"{(isTransparent ? "关闭" : "开启")}透明模式", null,
                    (s, args) => SetTransparentMode(!isTransparent));
                menu.Items.Add($"调整聊天区域透明度 (当前: {chatAreaAlpha})", null,
                    (s, args) => AdjustChatAreaAlpha());
                menu.Items.Add("退出程序", null, (s, args) => this.Close());
                menu.Show(this, e.Location);
            }
            base.OnMouseClick(e);
        }

        // 重写OnShown确保窗口正确显示
        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            this.BringToFront();
            this.Focus();
        }
    }
}