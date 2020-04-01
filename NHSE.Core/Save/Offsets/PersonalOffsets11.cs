﻿namespace NHSE.Core
{
    /// <summary>
    /// <inheritdoc cref="PersonalOffsets"/>
    /// </summary>
    public sealed class PersonalOffsets11 : PersonalOffsets
    {
        public override int PersonalId => 0xB0B8; // +0x18 from v1.0
        public override int Activity => 0xCF84; // +0x18 from v1.0
        public override int Wallet => 0x11590; // +0x18 from v1.0
        public override int NookMiles => 0x11588; // +0x18 from v1.0
        public override int Photo => 0x115C4; // +0x18 from v1.0

        public override int Pockets1 => 0x35C20; // +0x4C from v1.0
        public override int Pockets2 => Pockets1 + (8 * Pockets1Count) + 0x18;
        public override int Storage => Pockets2 + (8 * Pockets2Count) + 0x24;
        public override int ReceivedItems => 0x3FC68; // +0x4C from v1.0

        public override int Bank => 0x68C34; // +0x50 from v1.0
        public override int Recipes => 0x68C44; // + 0x50 from v1.0
    }
}