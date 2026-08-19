import ThemeTogglerTwo from "@/components/common/ThemeTogglerTwo";

import React from "react";

export default function AuthLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  return (
    <div className="relative z-1 min-h-screen bg-white dark:bg-gray-950">
      <div className="relative flex min-h-screen w-full flex-col overflow-hidden bg-white dark:bg-gray-950 lg:flex-row">
        {children}
        <div className="auth-hero-surface auth-assembly-hero relative hidden min-h-screen w-full items-center overflow-hidden bg-[#080b13] text-white lg:grid lg:w-1/2">
          <div className="absolute inset-0 z-0 bg-[url('/images/auth/login-engine-bg.png')] bg-cover bg-center opacity-70" />
          <div className="absolute inset-0 z-[1] bg-[radial-gradient(circle_at_76%_34%,rgba(230,0,40,0.22),transparent_28%),radial-gradient(circle_at_18%_82%,rgba(230,0,40,0.18),transparent_34%),linear-gradient(140deg,rgba(5,8,16,0.98)_0%,rgba(11,13,24,0.92)_43%,rgba(61,5,17,0.7)_70%,rgba(180,0,31,0.78)_100%)]" />
          <div className="auth-hero-grid absolute inset-0 z-[2]" />
          <div className="absolute inset-x-0 bottom-0 z-[2] h-[46%] bg-[linear-gradient(0deg,rgba(230,0,40,0.48),rgba(230,0,40,0.12)_52%,rgba(230,0,40,0))]" />
          <div className="absolute right-[-12%] top-[20%] z-[3] h-[42%] w-[72%] -skew-x-12 border-l border-white/10 bg-[#e60028]/12" />
          <div className="absolute right-[8%] top-[13%] z-[3] h-[58%] w-[36%] rotate-[18deg] border border-white/7 bg-white/[0.025]" />
          <div className="absolute bottom-[12%] left-[12%] z-[3] h-px w-[58%] bg-[linear-gradient(90deg,rgba(255,255,255,0.24),rgba(230,0,40,0.42),transparent)]" />

          <div className="relative z-10 flex items-center justify-center px-12">
            <div className="w-full max-w-[520px]">
              <div>
                <h2 className="text-5xl font-extrabold leading-tight tracking-normal text-white">
                  Smart Engine
                  <br />
                  <span className="text-[#ff2b38]">Assembly System</span>
                </h2>
                <p className="mt-8 max-w-[420px] text-xl leading-8 text-white/90">
                  Record tightening torque, cycle time, operator, and OK/NG judgement in one dashboard.
                </p>
              </div>

              <ul className="mt-9 space-y-5 text-base text-white/90">
                <li className="flex items-center gap-3"><span className="size-2 rounded-full bg-[#ff2b38]" />Estic nut runner traceability</li>
                <li className="flex items-center gap-3"><span className="size-2 rounded-full bg-[#ff2b38]" />Torque and angle master data</li>
                <li className="flex items-center gap-3"><span className="size-2 rounded-full bg-[#ff2b38]" />OK/NG result monitoring</li>
              </ul>

              <div className="mt-12 h-px w-full max-w-sm bg-white/15" />
              <p className="mt-9 max-w-sm text-lg italic leading-7 text-white/90">&quot;Accurate records for every assembly tightening process&quot;</p>
            </div>
          </div>
        </div>
      </div>
      <div className="fixed bottom-6 right-6 z-50">
        <ThemeTogglerTwo />
      </div>
    </div>
  );
}
