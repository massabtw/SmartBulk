import type { Metadata, Viewport } from "next";
import localFont from "next/font/local";
import "./globals.css";
import { BottomNav } from "@/components/BottomNav";

const geistSans = localFont({
  src: "./fonts/GeistVF.woff",
  variable: "--font-geist-sans",
  weight: "100 900",
});

const geistMono = localFont({
  src: "./fonts/GeistMonoVF.woff",
  variable: "--font-geist-mono",
  weight: "100 900",
});

export const metadata: Metadata = {
  title: "SmartBulk | Automação Nutricional",
  description: "Sistema pessoal de automação nutricional e cálculo reverso com refeitório para bulking.",
};

export const viewport: Viewport = {
  themeColor: "#09090b",
  width: "device-width",
  initialScale: 1,
  maximumScale: 1,
  userScalable: false,
};

export default function RootLayout({
  children,
}: Readonly<{
  children: React.ReactNode;
}>) {
  return (
    <html lang="pt-BR" className="dark">
      <body
        className={`${geistSans.variable} ${geistMono.variable} antialiased bg-background text-zinc-100 min-h-screen flex flex-col selection:bg-brand-500/30 selection:text-brand-100`}
      >
        <div className="flex-1 max-w-md w-full mx-auto pb-20 relative flex flex-col">
          {children}
        </div>
        <BottomNav />
      </body>
    </html>
  );
}
