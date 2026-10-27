"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { motion } from "framer-motion";
import {
  Flame,
  UtensilsCrossed,
  ShoppingCart,
  ChefHat,
  Settings,
} from "lucide-react";

interface NavItem {
  href: string;
  label: string;
  icon: typeof Flame;
}

const navItems: NavItem[] = [
  { href: "/", label: "Hoje", icon: Flame },
  { href: "/cardapio", label: "Cardápio", icon: UtensilsCrossed },
  { href: "/compras", label: "Compras", icon: ShoppingCart },
  { href: "/plano", label: "Plano", icon: ChefHat },
  { href: "/configuracoes", label: "Ajustes", icon: Settings },
];

export function BottomNav() {
  const pathname = usePathname();

  return (
    <nav className="fixed bottom-0 left-0 right-0 z-50 bg-zinc-950/95 border-t border-zinc-800/80 pb-safe">
      <div className="max-w-lg mx-auto px-4 h-16 flex items-center justify-around">
        {navItems.map((item) => {
          const isActive =
            item.href === "/"
              ? pathname === "/"
              : pathname.startsWith(item.href);

          const Icon = item.icon;

          return (
            <Link
              key={item.href}
              href={item.href}
              className="relative flex flex-col items-center justify-center flex-1 h-full py-1 text-xs select-none min-h-[44px]"
            >
              <motion.div
                whileTap={{ scale: 0.92 }}
                transition={{ type: "spring", stiffness: 400, damping: 20 }}
                className={`relative z-10 flex flex-col items-center gap-1 transition-colors duration-150 ${
                  isActive
                    ? "text-brand-400 font-semibold"
                    : "text-zinc-500 hover:text-zinc-300"
                }`}
              >
                <Icon
                  size={20}
                  strokeWidth={isActive ? 2.4 : 1.8}
                />
                <span className="text-[10px] tracking-tight">{item.label}</span>
              </motion.div>

              {isActive && (
                <div
                  className="absolute inset-x-2 inset-y-1 bg-brand-500/10 border border-brand-500/20 rounded-2xl pointer-events-none"
                />
              )}
            </Link>
          );
        })}
      </div>
    </nav>
  );
}

