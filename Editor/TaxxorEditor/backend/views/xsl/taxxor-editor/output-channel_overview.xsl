<?xml version="1.0" encoding="UTF-8"?>
<xsl:stylesheet xmlns:xsl="http://www.w3.org/1999/XSL/Transform" version="1.0">

    <xsl:param name="type">status</xsl:param>

    <xsl:output method="xml" omit-xml-declaration="yes" indent="yes"/>


    <xsl:template match="/">
        <outputchannels>
            <xsl:apply-templates select="/hierarchies/output_channel"/>
        </outputchannels>
    </xsl:template>

    <xsl:template match="output_channel">
        <outputchannel>
            <xsl:copy-of select="@*"/>
            <xsl:apply-templates select="name"/>
        </outputchannel>
    </xsl:template>

    <xsl:template match="name">
        <name>
            <xsl:value-of select="."/>
        </name>
    </xsl:template>

</xsl:stylesheet>
